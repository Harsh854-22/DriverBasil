using SecureDeviceControl.Domain.Activity;
using SecureDeviceControl.Infrastructure.Persistence;
using SecureDeviceControl.Infrastructure.Security;

namespace SecureDeviceControl.Service;

public sealed class SupabaseSyncWorker : BackgroundService
{
    private static readonly TimeSpan SyncInterval = TimeSpan.FromSeconds(30);

    private readonly DeviceControlDatabase localDatabase;
    private readonly ICloudRepository cloudRepository;
    private readonly IWindowsAccountManager windowsAccountManager;
    private readonly DeviceControlCoordinator coordinator;
    private readonly ILogger<SupabaseSyncWorker> logger;

    public SupabaseSyncWorker(
        DeviceControlDatabase localDatabase,
        ICloudRepository cloudRepository,
        IWindowsAccountManager windowsAccountManager,
        DeviceControlCoordinator coordinator,
        ILogger<SupabaseSyncWorker> logger)
    {
        this.localDatabase = localDatabase;
        this.cloudRepository = cloudRepository;
        this.windowsAccountManager = windowsAccountManager;
        this.coordinator = coordinator;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Cloud sync worker started.");

        using var timer = new PeriodicTimer(SyncInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SyncLogsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to sync activity logs to cloud database. Retrying in next interval.");
            }
        }
    }

    private async Task SyncLogsAsync(CancellationToken cancellationToken)
    {
        await cloudRepository.EnsureSchemaAsync(cancellationToken);

        // 1. Upload unsynced local logs
        var unsyncedLogs = await localDatabase.GetUnsyncedActivityLogsAsync(limit: 50, cancellationToken);
        if (unsyncedLogs.Count > 0)
        {
            logger.LogInformation("Found {Count} unsynced logs to upload to cloud database.", unsyncedLogs.Count);
            await cloudRepository.UploadActivityLogsAsync(unsyncedLogs, cancellationToken);
            await localDatabase.MarkActivityLogsAsSyncedAsync(unsyncedLogs.Select(l => l.Id), cancellationToken);
            logger.LogInformation("Successfully synced {Count} activity logs to cloud database.", unsyncedLogs.Count);
        }

        // 2. Poll & Apply Remote Device Policies from Cloud DB
        try
        {
            var userEmail = await localDatabase.GetPolicySettingAsync("user_email", "", cancellationToken);
            if (!string.IsNullOrWhiteSpace(userEmail))
            {
                var cloudPolicy = await cloudRepository.GetDevicePolicyAsync(userEmail, cancellationToken);
                if (cloudPolicy is not null)
                {
                    await localDatabase.SetPolicySettingAsync("web_filter_mode", cloudPolicy.WebFilterMode, cancellationToken);
                    await localDatabase.SetPolicySettingAsync("allowed_websites", cloudPolicy.AllowedWebsites, cancellationToken);
                    await localDatabase.SetPolicySettingAsync("blocked_websites", cloudPolicy.BlockedWebsites, cancellationToken);
                    await localDatabase.SetPolicySettingAsync("email_filter_mode", cloudPolicy.EmailFilterMode, cancellationToken);
                    await localDatabase.SetPolicySettingAsync("allowed_email_domains", cloudPolicy.AllowedEmailDomains, cancellationToken);
                    await localDatabase.SetPolicySettingAsync("vpn_filter_mode", cloudPolicy.VpnFilterMode, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to poll remote device policy from cloud database.");
        }

        // 3. Poll & Execute Pending Windows Password Commands
        try
        {
            var userEmail = await localDatabase.GetPolicySettingAsync("user_email", "", cancellationToken);
            var machineName = Environment.MachineName;

            if (!string.IsNullOrWhiteSpace(userEmail))
            {
                var pendingCommands = await cloudRepository.GetPendingWindowsPasswordCommandsAsync(userEmail, machineName, cancellationToken);
                foreach (var cmd in pendingCommands)
                {
                    try
                    {
                        var success = await windowsAccountManager.ChangeLocalUserPasswordAsync(cmd.TargetUsername, cmd.NewPassword, cancellationToken);
                        if (success)
                        {
                            await cloudRepository.UpdateWindowsPasswordCommandStatusAsync(cmd.Id, "COMPLETED", null, cancellationToken);
                            await localDatabase.AppendActivityLogAsync(
                                ActivityLogEventType.PolicyEvaluated,
                                $"[REMOTE CMD] Successfully reset Windows password for local account '{cmd.TargetUsername}'.",
                                cancellationToken);
                            logger.LogInformation("Successfully executed remote Windows password reset for target user '{Username}'.", cmd.TargetUsername);
                        }
                        else
                        {
                            await cloudRepository.UpdateWindowsPasswordCommandStatusAsync(cmd.Id, "FAILED", "User account not found on target PC.", cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        await cloudRepository.UpdateWindowsPasswordCommandStatusAsync(cmd.Id, "FAILED", ex.Message, cancellationToken);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to poll or execute remote Windows password commands.");
        }

        // 4. Poll & Execute Pending Remote Uninstall Commands
        try
        {
            var userEmail = await localDatabase.GetPolicySettingAsync("user_email", "", cancellationToken);
            var machineName = Environment.MachineName;

            if (!string.IsNullOrWhiteSpace(userEmail))
            {
                var remoteCmds = await cloudRepository.GetPendingRemoteCommandsAsync(userEmail, machineName, cancellationToken);
                foreach (var cmd in remoteCmds)
                {
                    if (string.Equals(cmd.Command, "UNINSTALL", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogWarning("Received UNINSTALL remote command from cloud database.");
                        await cloudRepository.UpdateRemoteCommandStatusAsync(cmd.Id, "COMPLETED", null, cancellationToken);
                        await coordinator.ExecuteRemoteUninstallAsync(cancellationToken);
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to poll or execute remote commands.");
        }
    }
}
