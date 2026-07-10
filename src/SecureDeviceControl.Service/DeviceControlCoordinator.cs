using System.Security.Cryptography;
using System.Text.Json;
using SecureDeviceControl.Domain.Activity;
using SecureDeviceControl.Domain.Policy;
using SecureDeviceControl.Domain.Security;
using SecureDeviceControl.Infrastructure.Paths;
using SecureDeviceControl.Infrastructure.Persistence;
using SecureDeviceControl.Infrastructure.Security;
using SecureDeviceControl.Infrastructure.Usb;
using SecureDeviceControl.Service.Ipc;
using SecureDeviceControl.Shared.Contracts;
using SecureDeviceControl.Shared.Ipc;
using SecureDeviceControl.Shared.Security;

namespace SecureDeviceControl.Service;

public sealed class DeviceControlCoordinator
{
    private static readonly TimeSpan PolicyInterval = TimeSpan.FromSeconds(5);

    private readonly DeviceControlDatabase database;
    private readonly IPinHasher pinHasher;
    private readonly ISecretProtector secretProtector;
    private readonly IUsbStoragePolicy usbStoragePolicy;
    private readonly ProgramDataPaths paths;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<DeviceControlCoordinator> logger;
    private readonly SemaphoreSlim gate = new(1, 1);

    private DateTimeOffset? unlockExpiresAt;

    public DeviceControlCoordinator(
        DeviceControlDatabase database,
        IPinHasher pinHasher,
        ISecretProtector secretProtector,
        IUsbStoragePolicy usbStoragePolicy,
        ProgramDataPaths paths,
        TimeProvider timeProvider,
        ILogger<DeviceControlCoordinator> logger)
    {
        this.database = database;
        this.pinHasher = pinHasher;
        this.secretProtector = secretProtector;
        this.usbStoragePolicy = usbStoragePolicy;
        this.paths = paths;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await database.InitializeAsync(cancellationToken);
        await LockUsbStorageAsync(cancellationToken);
        await database.AppendActivityLogAsync(
            ActivityLogEventType.ServiceStarted,
            "Secure Device Control service started and applied pendrive lock policy.",
            cancellationToken);
    }

    public async Task RunPolicyLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(PolicyInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            if (!IsUnlockActive(timeProvider.GetUtcNow()))
            {
                await LockUsbStorageAsync(cancellationToken);
            }
        }
    }

    public async Task<ServiceStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var isInitialized = await database.HasPinCredentialsAsync(cancellationToken);
        var isLocked = await usbStoragePolicy.IsUsbStorageLockedAsync(cancellationToken);

        return new ServiceStatusDto(
            isInitialized,
            isLocked,
            IsUnlockActive(now),
            IsUnlockActive(now) ? unlockExpiresAt : null);
    }

    public async Task InitializePinsAsync(
        string deviceUnlockPin,
        string uninstallPin,
        CancellationToken cancellationToken)
    {
        if (await database.HasPinCredentialsAsync(cancellationToken))
        {
            throw new IpcRequestException(IpcErrorCode.AlreadyInitialized, "PINs have already been initialized.");
        }

        if (!PinPolicy.IsValid(deviceUnlockPin) || !PinPolicy.IsValid(uninstallPin))
        {
            throw new IpcRequestException(IpcErrorCode.BadRequest, "Both PINs must be exactly 6 digits.");
        }

        if (deviceUnlockPin == uninstallPin)
        {
            throw new IpcRequestException(IpcErrorCode.BadRequest, "Device unlock PIN and uninstall PIN must be different.");
        }

        await database.SetPinCredentialAsync(
            PinPurpose.DeviceUnlock,
            pinHasher.Hash(deviceUnlockPin),
            cancellationToken);
        await database.SetPinCredentialAsync(
            PinPurpose.Uninstall,
            pinHasher.Hash(uninstallPin),
            cancellationToken);
        await database.AppendActivityLogAsync(
            ActivityLogEventType.PinsInitialized,
            "Device unlock and uninstall PINs were initialized.",
            cancellationToken);
        await LockUsbStorageAsync(cancellationToken);
    }

    public async Task<bool> ValidatePinAsync(
        PinPurpose purpose,
        string pin,
        CancellationToken cancellationToken)
    {
        if (!PinPolicy.IsValid(pin))
        {
            await database.AppendActivityLogAsync(
                ActivityLogEventType.InvalidPin,
                $"{purpose} PIN validation failed.",
                cancellationToken);
            return false;
        }

        var credential = await database.GetPinCredentialAsync(purpose, cancellationToken);
        if (credential is null)
        {
            throw new IpcRequestException(IpcErrorCode.NotInitialized, "PINs have not been initialized.");
        }

        var isValid = pinHasher.Verify(pin, credential);
        await database.AppendActivityLogAsync(
            isValid ? ActivityLogEventType.PinValidated : ActivityLogEventType.InvalidPin,
            isValid ? $"{purpose} PIN was validated." : $"{purpose} PIN validation failed.",
            cancellationToken);
        return isValid;
    }

    public async Task<StartUnlockTimerResult> StartUnlockTimerAsync(
        int minutes,
        CancellationToken cancellationToken)
    {
        if (minutes is < 1 or > 120)
        {
            throw new IpcRequestException(IpcErrorCode.BadRequest, "Unlock timer must be between 1 and 120 minutes.");
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            unlockExpiresAt = timeProvider.GetUtcNow().AddMinutes(minutes);
            await usbStoragePolicy.SetUsbStorageLockedAsync(locked: false, cancellationToken);
            await database.AppendActivityLogAsync(
                ActivityLogEventType.DeviceUnlockStarted,
                $"Pendrive access unlocked until {unlockExpiresAt:O}.",
                cancellationToken);
            return new StartUnlockTimerResult(unlockExpiresAt.Value);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<UninstallAuthorizationResult> IssueUninstallAuthorizationAsync(CancellationToken cancellationToken)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes);
        var expiresAt = timeProvider.GetUtcNow().AddMinutes(10);
        var tokenHash = Convert.ToBase64String(SHA256.HashData(tokenBytes));
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            new UninstallAuthorizationFile(tokenHash, expiresAt),
            IpcJson.Options);
        var protectedPayload = secretProtector.Protect(payload);

        paths.EnsureBaseDirectory();
        await File.WriteAllBytesAsync(paths.UninstallAuthorizationPath, protectedPayload, cancellationToken);
        await database.AppendActivityLogAsync(
            ActivityLogEventType.UninstallAuthorizationIssued,
            $"Uninstall authorization issued until {expiresAt:O}.",
            cancellationToken);

        return new UninstallAuthorizationResult(token, expiresAt);
    }

    public Task<IReadOnlyList<ActivityLogDto>> ListActivityLogsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        return database.ListActivityLogsAsync(limit, cancellationToken);
    }

    private async Task LockUsbStorageAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (IsUnlockActive(timeProvider.GetUtcNow()))
            {
                return;
            }

            await usbStoragePolicy.SetUsbStorageLockedAsync(locked: true, cancellationToken);
            await database.AppendActivityLogAsync(
                ActivityLogEventType.DeviceLockApplied,
                "Pendrive access lock policy was applied.",
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply pendrive lock policy.");
            throw;
        }
        finally
        {
            gate.Release();
        }
    }

    private bool IsUnlockActive(DateTimeOffset now)
    {
        return new UnlockTimer(unlockExpiresAt).IsActiveAt(now);
    }

    private sealed record UninstallAuthorizationFile(
        string TokenHash,
        DateTimeOffset ExpiresAt);
}
