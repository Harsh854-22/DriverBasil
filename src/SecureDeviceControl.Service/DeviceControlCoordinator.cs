using System.Security.Cryptography;
using System.Text;
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
using SecureDeviceControl.Service.Snapshots;
using SecureDeviceControl.Shared.Contracts;
using SecureDeviceControl.Shared.Ipc;
using SecureDeviceControl.Shared.Security;
using SecureDeviceControl.Shared;
using SecureDeviceControl.Shared.Snapshots;

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
    private readonly ISnapshotStorage? snapshotStorage;
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
        ILogger<DeviceControlCoordinator> logger,
        ISnapshotStorage? snapshotStorage = null)
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
        this.snapshotStorage = snapshotStorage;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await database.InitializeAsync(cancellationToken);
        blockServer.StartServer(8085);
        var userEmail = await database.GetPolicySettingAsync("user_email", "", cancellationToken);
        blockServer.UpdateUserContext(userEmail, Environment.MachineName);

        SnapshotPersistence.RemoveLogonResume();
        await EnsureSnapshotsOffByDefaultAsync(cancellationToken);
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
        var isInitialized = await IsRegistrationCompleteAsync(cancellationToken);
        var isUsbLocked = await usbStoragePolicy.IsUsbStorageLockedAsync(cancellationToken);
        var isMobileLocked = await mobilePortPolicy.IsMobilePortLockedAsync(cancellationToken);

        var userEmail = await database.GetPolicySettingAsync("user_email", "", cancellationToken);
        var machineName = Environment.MachineName;
        var webFilterMode = await database.GetPolicySettingAsync("web_filter_mode", "OFF", cancellationToken);
        var emailFilterMode = await database.GetPolicySettingAsync("email_filter_mode", "OFF", cancellationToken);
        var snapshotEnabled = await database.GetPolicySettingAsync("snapshot_enabled", "false", cancellationToken);
        var snapshotInterval = await database.GetPolicySettingAsync("snapshot_interval_minutes", "5", cancellationToken);
        var snapshotAcknowledged = await database.GetPolicySettingAsync("snapshot_monitoring_acknowledged", "false", cancellationToken);
        var snapshotIntervalMinutes = int.TryParse(snapshotInterval, out var configuredInterval)
            ? Math.Clamp(configuredInterval, 1, 120)
            : 5;

        return new ServiceStatusDto(
            isInitialized,
            isUsbLocked,
            isMobileLocked,
            IsUnlockActive(now),
            IsUnlockActive(now) ? unlockExpiresAt : null,
            userEmail,
            machineName,
            webFilterMode,
            emailFilterMode,
            string.Equals(snapshotEnabled, "true", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(snapshotAcknowledged, "true", StringComparison.OrdinalIgnoreCase),
            snapshotIntervalMinutes,
            string.Equals(snapshotAcknowledged, "true", StringComparison.OrdinalIgnoreCase));
    }

    public async Task InitializePinsAsync(
        string userEmail,
        string deviceUnlockPin,
        string uninstallPin,
        CancellationToken cancellationToken,
        bool snapshotMonitoringAcknowledged = false)
    {
        if (await IsRegistrationCompleteAsync(cancellationToken))
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

        await database.SetPinCredentialAsync(
            PinPurpose.DeviceUnlock,
            pinHasher.Hash(deviceUnlockPin),
            cancellationToken);
        await database.SetPinCredentialAsync(
            PinPurpose.Uninstall,
            pinHasher.Hash(uninstallPin),
            cancellationToken);
        await database.SetPolicySettingAsync("user_email", userEmail.Trim().ToLowerInvariant(), cancellationToken);
        await database.SetPolicySettingAsync("cloud_registration_pending", "true", cancellationToken);
        await database.SetPolicySettingAsync("snapshot_monitoring_acknowledged", snapshotMonitoringAcknowledged ? "true" : "false", cancellationToken);
        await database.SetPolicySettingAsync("snapshot_enabled", snapshotMonitoringAcknowledged ? "true" : "false", cancellationToken);
        await database.SetPolicySettingAsync("snapshot_interval_minutes", "5", cancellationToken);
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

    public async Task UploadSnapshotAsync(
        UploadSnapshotRequest snapshot,
        CancellationToken cancellationToken)
    {
        const int maximumSnapshotBytes = 8 * 1024 * 1024;

        if (!await IsRegistrationCompleteAsync(cancellationToken))
        {
            throw new IpcRequestException(IpcErrorCode.NotInitialized, "The PC must be registered before snapshots can be uploaded.");
        }

        if (snapshot.ImageBytes.Length is < 4 or > maximumSnapshotBytes ||
            snapshot.ImageBytes[0] != 0xFF || snapshot.ImageBytes[1] != 0xD8 ||
            snapshot.ImageBytes[^2] != 0xFF || snapshot.ImageBytes[^1] != 0xD9)
        {
            throw new IpcRequestException(IpcErrorCode.BadRequest, "Snapshot data must be a JPEG image no larger than 8 MB.");
        }

        var acknowledged = await database.GetPolicySettingAsync("snapshot_monitoring_acknowledged", "false", cancellationToken);
        var enabled = await database.GetPolicySettingAsync("snapshot_enabled", "false", cancellationToken);
        if (!string.Equals(acknowledged, "true", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            throw new IpcRequestException(IpcErrorCode.Unauthorized, "Screenshot monitoring is not enabled for this PC.");
        }

        var intervalValue = await database.GetPolicySettingAsync("snapshot_interval_minutes", "5", cancellationToken);
        var intervalMinutes = int.TryParse(intervalValue, out var configuredInterval)
            ? Math.Clamp(configuredInterval, 1, 120)
            : 5;
        var now = timeProvider.GetUtcNow();
        var lastUploadedValue = await database.GetPolicySettingAsync("last_snapshot_uploaded_at", "", cancellationToken);
        if (DateTimeOffset.TryParse(lastUploadedValue, out var lastUploadedAt) && now - lastUploadedAt < TimeSpan.FromMinutes(intervalMinutes))
        {
            throw new IpcRequestException(IpcErrorCode.RateLimited, "The next snapshot is not due yet.");
        }

        var objectKey = SnapshotObjectKey.Build(Environment.MachineName, now);

        try
        {
            if (snapshotStorage is null)
            {
                throw new InvalidOperationException("Snapshot storage is unavailable.");
            }

            await snapshotStorage.UploadAsync(objectKey, snapshot.ImageBytes, cancellationToken);
            await database.SetPolicySettingAsync("last_snapshot_uploaded_at", now.ToString("O"), cancellationToken);
            await database.AppendActivityLogAsync(ActivityLogEventType.SnapshotUploaded, "A screen snapshot was uploaded to company storage.", cancellationToken);
        }
        catch (IpcRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Snapshot upload failed.");
            await database.AppendActivityLogAsync(ActivityLogEventType.SnapshotUploadFailed, "A screen snapshot could not be uploaded.", cancellationToken);
            throw new IpcRequestException(IpcErrorCode.InternalError, "The snapshot could not be uploaded.");
        }
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
            if (!await IsRegistrationCompleteAsync(cancellationToken))
            {
                unlockExpiresAt = null;
                await usbStoragePolicy.SetUsbStorageLockedAsync(locked: false, cancellationToken);
                await mobilePortPolicy.SetMobilePortLockedAsync(locked: false, cancellationToken);
                return;
            }

            var now = timeProvider.GetUtcNow();
            if (IsUnlockActive(now))
            {
                await usbStoragePolicy.SetUsbStorageLockedAsync(locked: false, cancellationToken);
                await mobilePortPolicy.SetMobilePortLockedAsync(locked: false, cancellationToken);
            }
            else
            {
                var usbStorageLockedSetting = await database.GetPolicySettingAsync("usb_storage_locked", "true", cancellationToken);
                var mobilePortLockedSetting = await database.GetPolicySettingAsync("mobile_port_locked", "true", cancellationToken);

                await usbStoragePolicy.SetUsbStorageLockedAsync(usbStorageLockedSetting == "true", cancellationToken);
                await mobilePortPolicy.SetMobilePortLockedAsync(mobilePortLockedSetting == "true", cancellationToken);
            }

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

    public async Task ExecuteRemoteUninstallAsync(string payload, CancellationToken cancellationToken)
    {
        logger.LogWarning("Executing remote software uninstallation command...");

        var options = ParseRemoteUninstallOptions(payload);
        SnapshotPersistence.RemoveLogonResume();
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
            var encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(BuildRemoteUninstallScript(options.RemoveData)));
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand {encodedScript}",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            System.Diagnostics.Process.Start(startInfo);
        }
    }

    public Task ExecuteRemoteUninstallAsync(CancellationToken cancellationToken)
    {
        return ExecuteRemoteUninstallAsync("", cancellationToken);
    }

    private string BuildRemoteUninstallScript(bool removeData)
    {
        var installedDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var canRemoveInstalledDirectory =
            OperatingSystem.IsWindows() &&
            !string.IsNullOrWhiteSpace(programFiles) &&
            installedDirectory.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Path.GetFileName(installedDirectory), "SecureDeviceControl", StringComparison.OrdinalIgnoreCase);

        var installDirectoryLiteral = canRemoveInstalledDirectory
            ? ToPowerShellSingleQuotedLiteral(installedDirectory)
            : "''";
        var programDataLiteral = ToPowerShellSingleQuotedLiteral(paths.BaseDirectory);
        var serviceNameLiteral = ToPowerShellSingleQuotedLiteral(ServiceIdentity.ServiceName);
        var removeDataLiteral = removeData ? "$true" : "$false";

        return $$"""
            $ErrorActionPreference = 'SilentlyContinue'
            $serviceName = {{serviceNameLiteral}}
            $installDirectory = {{installDirectoryLiteral}}
            $programDataDirectory = {{programDataLiteral}}
            $removeData = {{removeDataLiteral}}

            Start-Sleep -Seconds 3
            Stop-Service -Name $serviceName -Force
            sc.exe delete $serviceName | Out-Null

            schtasks.exe /Delete /TN 'SecureDeviceControlSnapshots' /F | Out-Null
            Remove-ItemProperty -Path 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'SecureDeviceControlDesktop' -ErrorAction SilentlyContinue
            Remove-ItemProperty -Path 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'SecureDeviceControlSnapshots' -ErrorAction SilentlyContinue
            Get-ChildItem -Path Registry::HKEY_USERS | ForEach-Object {
                $runPath = "Registry::$($_.Name)\Software\Microsoft\Windows\CurrentVersion\Run"
                Remove-ItemProperty -Path $runPath -Name 'SecureDeviceControlDesktop' -ErrorAction SilentlyContinue
                Remove-ItemProperty -Path $runPath -Name 'SecureDeviceControlSnapshots' -ErrorAction SilentlyContinue
            }

            if (-not [string]::IsNullOrWhiteSpace($installDirectory) -and (Test-Path -LiteralPath $installDirectory)) {
                Remove-Item -LiteralPath $installDirectory -Recurse -Force
            }

            if ($removeData -and (Test-Path -LiteralPath $programDataDirectory)) {
                Remove-Item -LiteralPath $programDataDirectory -Recurse -Force
            }
            """;
    }

    private static string ToPowerShellSingleQuotedLiteral(string value)
    {
        return $"'{value.Replace("'", "''")}'";
    }

    private static RemoteUninstallOptions ParseRemoteUninstallOptions(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return new RemoteUninstallOptions(false);
        }

        try
        {
            var options = JsonSerializer.Deserialize<RemoteUninstallOptions>(payload, IpcJson.Options);
            return options ?? new RemoteUninstallOptions(false);
        }
        catch
        {
            return new RemoteUninstallOptions(false);
        }
    }

    private bool IsUnlockActive(DateTimeOffset now)
    {
        return new UnlockTimer(unlockExpiresAt).IsActiveAt(now);
    }

    /// <summary>
    /// Fleet-wide snapshots OFF: every service start forces local
    /// <c>snapshot_enabled=false</c> so existing PCs stop capturing even before
    /// the cloud policy syncs. Port locks are NOT touched here — they are applied
    /// separately by <c>ApplyPolicyStatesAsync</c> (defaults "true" = blocked).
    /// Explicit re-enable remains possible only via cloud
    /// <c>device_policies.snapshot_enabled=true</c> (see SupabaseSyncWorker).
    /// </summary>
    public async Task EnsureSnapshotsOffByDefaultAsync(CancellationToken cancellationToken)
    {
        await database.SetPolicySettingAsync("snapshot_enabled", "false", cancellationToken);
    }

    public async Task ForceSnapshotsOnIfRegisteredAsync(CancellationToken cancellationToken)
    {
        if (!await IsRegistrationCompleteAsync(cancellationToken))
        {
            return;
        }

        await database.SetPolicySettingAsync("snapshot_enabled", "true", cancellationToken);
        await database.SetPolicySettingAsync("snapshot_monitoring_acknowledged", "true", cancellationToken);
        var interval = await database.GetPolicySettingAsync("snapshot_interval_minutes", "", cancellationToken);
        if (!int.TryParse(interval, out var minutes) || minutes is < 1 or > 120)
        {
            await database.SetPolicySettingAsync("snapshot_interval_minutes", "5", cancellationToken);
        }

        SnapshotPersistence.EnsureLogonResume();
        SnapshotPersistence.TryStartAgentNow();
    }

    private async Task<bool> IsRegistrationCompleteAsync(CancellationToken cancellationToken)
    {
        if (!await database.HasPinCredentialsAsync(cancellationToken))
        {
            return false;
        }

        var userEmail = await database.GetPolicySettingAsync("user_email", "", cancellationToken);
        return !string.IsNullOrWhiteSpace(userEmail) && userEmail.Contains('@') && userEmail.Contains('.');
    }

    private sealed record UninstallAuthorizationFile(
        string TokenHash,
        DateTimeOffset ExpiresAt);

    private sealed record RemoteUninstallOptions(bool RemoveData = false);
}
