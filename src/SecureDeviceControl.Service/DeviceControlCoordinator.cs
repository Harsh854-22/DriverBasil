using System.Security.Cryptography;
using System.Text.Json;
using SecureDeviceControl.Domain.Activity;
using SecureDeviceControl.Domain.Policy;
using SecureDeviceControl.Domain.Security;
using SecureDeviceControl.Infrastructure.Paths;
using SecureDeviceControl.Infrastructure.Persistence;
using SecureDeviceControl.Infrastructure.Security;
using SecureDeviceControl.Infrastructure.Usb;
using SecureDeviceControl.Infrastructure.Web;
using SecureDeviceControl.Infrastructure.Vpn;
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
    private readonly IMobilePortPolicy mobilePortPolicy;
    private readonly IWebFilterPolicy webFilterPolicy;
    private readonly IVpnFilterPolicy vpnFilterPolicy;
    private readonly RestrictedAccessBlockServer blockServer;
    private readonly ICloudRepository cloudRepository;
    private readonly IRemovableDriveMonitor removableDriveMonitor;
    private readonly ProgramDataPaths paths;
    private readonly TimeProvider timeProvider;
    private readonly IConfiguration configuration;
    private readonly ILogger<DeviceControlCoordinator> logger;
    private readonly SemaphoreSlim gate = new(1, 1);

    private DateTimeOffset? unlockExpiresAt;

    public DeviceControlCoordinator(
        DeviceControlDatabase database,
        IPinHasher pinHasher,
        ISecretProtector secretProtector,
        IUsbStoragePolicy usbStoragePolicy,
        IMobilePortPolicy mobilePortPolicy,
        IWebFilterPolicy webFilterPolicy,
        IVpnFilterPolicy vpnFilterPolicy,
        RestrictedAccessBlockServer blockServer,
        ICloudRepository cloudRepository,
        IRemovableDriveMonitor removableDriveMonitor,
        ProgramDataPaths paths,
        TimeProvider timeProvider,
        IConfiguration configuration,
        ILogger<DeviceControlCoordinator> logger)
    {
        this.database = database;
        this.pinHasher = pinHasher;
        this.secretProtector = secretProtector;
        this.usbStoragePolicy = usbStoragePolicy;
        this.mobilePortPolicy = mobilePortPolicy;
        this.webFilterPolicy = webFilterPolicy;
        this.vpnFilterPolicy = vpnFilterPolicy;
        this.blockServer = blockServer;
        this.cloudRepository = cloudRepository;
        this.removableDriveMonitor = removableDriveMonitor;
        this.paths = paths;
        this.timeProvider = timeProvider;
        this.configuration = configuration;
        this.logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await database.InitializeAsync(cancellationToken);
        blockServer.StartServer(8085);
        var userEmail = await database.GetPolicySettingAsync("user_email", "", cancellationToken);
        blockServer.UpdateUserContext(userEmail, Environment.MachineName);

        await ApplyPolicyStatesAsync(cancellationToken);
        removableDriveMonitor.StartMonitoring((fileName, sizeInBytes, driveLetter) =>
        {
            var sizeKb = Math.Max(1, sizeInBytes / 1024);
            _ = database.AppendActivityLogAsync(
                ActivityLogEventType.FileTransferDetected,
                $"[FILE WRITE] '{fileName}' ({sizeKb} KB) written to removable drive {driveLetter}.",
                CancellationToken.None);
        });
        await database.AppendActivityLogAsync(
            ActivityLogEventType.ServiceStarted,
            "Secure Device Control service started and applied hardware lock policies.",
            cancellationToken);
    }

    public async Task RunPolicyLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(PolicyInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await ApplyPolicyStatesAsync(cancellationToken);
        }
    }

    public async Task<ServiceStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var isInitialized = await database.HasPinCredentialsAsync(cancellationToken);
        var isUsbLocked = await usbStoragePolicy.IsUsbStorageLockedAsync(cancellationToken);
        var isMobileLocked = await mobilePortPolicy.IsMobilePortLockedAsync(cancellationToken);

        var userEmail = await database.GetPolicySettingAsync("user_email", "", cancellationToken);
        var machineName = Environment.MachineName;
        var webFilterMode = await database.GetPolicySettingAsync("web_filter_mode", "OFF", cancellationToken);
        var emailFilterMode = await database.GetPolicySettingAsync("email_filter_mode", "OFF", cancellationToken);

        return new ServiceStatusDto(
            isInitialized,
            isUsbLocked,
            isMobileLocked,
            IsUnlockActive(now),
            IsUnlockActive(now) ? unlockExpiresAt : null,
            userEmail,
            machineName,
            webFilterMode,
            emailFilterMode);
    }

    public async Task InitializePinsAsync(
        string userEmail,
        string deviceUnlockPin,
        string uninstallPin,
        CancellationToken cancellationToken)
    {
        if (await database.HasPinCredentialsAsync(cancellationToken))
        {
            throw new IpcRequestException(IpcErrorCode.AlreadyInitialized, "PINs have already been initialized.");
        }

        if (string.IsNullOrWhiteSpace(userEmail) || !userEmail.Contains('@') || !userEmail.Contains('.'))
        {
            throw new IpcRequestException(IpcErrorCode.BadRequest, "A valid Email ID is required for registration.");
        }

        if (!PinPolicy.IsValid(deviceUnlockPin) || !PinPolicy.IsValid(uninstallPin))
        {
            throw new IpcRequestException(IpcErrorCode.BadRequest, "Both PINs must be exactly 6 digits.");
        }

        if (deviceUnlockPin == uninstallPin)
        {
            throw new IpcRequestException(IpcErrorCode.BadRequest, "Device unlock PIN and uninstall PIN must be different.");
        }

        var machineName = Environment.MachineName;

        await database.SetPolicySettingAsync("user_email", userEmail.Trim().ToLowerInvariant(), cancellationToken);
        await database.SetPolicySettingAsync("cloud_registration_pending", "true", cancellationToken);

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
            $"Device protection initialized for Email '{userEmail}' on PC '{machineName}'.",
            cancellationToken);
        await ApplyPolicyStatesAsync(cancellationToken);

        // Fire-and-forget background cloud registration attempt so UI responds instantly (<0.1s)
        _ = Task.Run(async () =>
        {
            try
            {
                using var cloudCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                await cloudRepository.RegisterDeviceAsync(userEmail, machineName, cloudCts.Token);
                await database.SetPolicySettingAsync("cloud_registration_pending", "false", CancellationToken.None);
                logger.LogInformation("Background cloud registration succeeded for '{Email}'.", userEmail);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Background cloud registration attempt failed. SupabaseSyncWorker will retry automatically.");
            }
        }, CancellationToken.None);
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
            await mobilePortPolicy.SetMobilePortLockedAsync(locked: false, cancellationToken);
            await database.AppendActivityLogAsync(
                ActivityLogEventType.DeviceUnlockStarted,
                $"Pendrive and mobile access unlocked until {unlockExpiresAt:O}.",
                cancellationToken);
            return new StartUnlockTimerResult(unlockExpiresAt.Value);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SetDeviceClassLockAsync(
        DeviceClass deviceClass,
        bool locked,
        CancellationToken cancellationToken)
    {
        if (deviceClass == DeviceClass.Unknown)
        {
            throw new IpcRequestException(IpcErrorCode.BadRequest, "Unknown device class specified.");
        }

        var settingKey = deviceClass switch
        {
            DeviceClass.RemovableStorage => "usb_storage_locked",
            DeviceClass.MobileDevice => "mobile_port_locked",
            _ => throw new ArgumentOutOfRangeException(nameof(deviceClass))
        };

        var settingValue = locked ? "true" : "false";
        await database.SetPolicySettingAsync(settingKey, settingValue, cancellationToken);

        // Reset timed unlock when manually applying policies so state takes effect immediately
        unlockExpiresAt = null;

        await ApplyPolicyStatesAsync(cancellationToken);

        var statusMsg = locked ? "locked" : "unlocked";
        await database.AppendActivityLogAsync(
            ActivityLogEventType.DeviceLockApplied,
            $"{deviceClass} was manually {statusMsg}.",
            cancellationToken);
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

    private async Task ApplyPolicyStatesAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow();
            if (IsUnlockActive(now))
            {
                await usbStoragePolicy.SetUsbStorageLockedAsync(locked: false, cancellationToken);
                await mobilePortPolicy.SetMobilePortLockedAsync(locked: false, cancellationToken);
                return;
            }

            var usbStorageLockedSetting = await database.GetPolicySettingAsync("usb_storage_locked", "true", cancellationToken);
            var mobilePortLockedSetting = await database.GetPolicySettingAsync("mobile_port_locked", "true", cancellationToken);

            var usbLocked = usbStorageLockedSetting == "true";
            var mobileLocked = mobilePortLockedSetting == "true";

            await usbStoragePolicy.SetUsbStorageLockedAsync(usbLocked, cancellationToken);
            await mobilePortPolicy.SetMobilePortLockedAsync(mobileLocked, cancellationToken);

            var webModeStr = await database.GetPolicySettingAsync("web_filter_mode", "OFF", cancellationToken);
            var allowedWeb = await database.GetPolicySettingAsync("allowed_websites", "", cancellationToken);
            var blockedWeb = await database.GetPolicySettingAsync("blocked_websites", "", cancellationToken);
            var emailModeStr = await database.GetPolicySettingAsync("email_filter_mode", "OFF", cancellationToken);
            var allowedEmail = await database.GetPolicySettingAsync("allowed_email_domains", "company.com", cancellationToken);

            var webMode = Enum.TryParse<WebFilterMode>(webModeStr, ignoreCase: true, out var wm) ? wm : WebFilterMode.Off;
            var emailMode = Enum.TryParse<EmailFilterMode>(emailModeStr, ignoreCase: true, out var em) ? em : EmailFilterMode.Off;

            var allowedWebList = allowedWeb.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var blockedWebList = blockedWeb.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var allowedEmailList = allowedEmail.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var vpnModeStr = await database.GetPolicySettingAsync("vpn_filter_mode", "OFF", cancellationToken);
            var vpnMode = Enum.TryParse<VpnFilterMode>(vpnModeStr, ignoreCase: true, out var vm) ? vm : VpnFilterMode.Off;

            await webFilterPolicy.ApplyWebFilterPolicyAsync(
                webMode,
                allowedWebList,
                blockedWebList,
                emailMode,
                allowedEmailList,
                cancellationToken);

            await vpnFilterPolicy.ApplyVpnFilterPolicyAsync(vpnMode, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply hardware lock policies.");
            throw;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task ExecuteRemoteUninstallAsync(CancellationToken cancellationToken)
    {
        logger.LogWarning("Executing remote software uninstallation command...");

        await usbStoragePolicy.SetUsbStorageLockedAsync(locked: false, cancellationToken);
        await mobilePortPolicy.SetMobilePortLockedAsync(locked: false, cancellationToken);
        await webFilterPolicy.ApplyWebFilterPolicyAsync(
            WebFilterMode.Off, Array.Empty<string>(), Array.Empty<string>(),
            EmailFilterMode.Off, Array.Empty<string>(), cancellationToken);

        await database.AppendActivityLogAsync(
            ActivityLogEventType.UninstallAuthorizationIssued,
            "Remote software uninstallation command executed. Software unregistered and ports unlocked.",
            cancellationToken);

        if (OperatingSystem.IsWindows())
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c timeout /t 2 && sc stop \"Secure Device Control\" && sc delete \"Secure Device Control\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            System.Diagnostics.Process.Start(startInfo);
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
