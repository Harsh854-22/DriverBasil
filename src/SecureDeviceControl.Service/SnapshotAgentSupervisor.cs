using SecureDeviceControl.Infrastructure.Persistence;
using SecureDeviceControl.Infrastructure.Updates;
using SecureDeviceControl.Service.Snapshots;

namespace SecureDeviceControl.Service;

public sealed class SnapshotAgentSupervisor : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(20);

    private readonly DeviceControlDatabase database;
    private readonly ICloudRepository cloudRepository;
    private readonly ISoftwareUpdater softwareUpdater;
    private readonly ILogger<SnapshotAgentSupervisor> logger;

    public SnapshotAgentSupervisor(
        DeviceControlDatabase database,
        ICloudRepository cloudRepository,
        ISoftwareUpdater softwareUpdater,
        ILogger<SnapshotAgentSupervisor> logger)
    {
        this.database = database;
        this.cloudRepository = cloudRepository;
        this.softwareUpdater = softwareUpdater;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Snapshot agent supervisor started. Logon/boot resume will be repaired continuously.");

        await RepairAndResumeAsync(stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RepairAndResumeAsync(stoppingToken);
        }
    }

    private async Task RepairAndResumeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var acknowledged = await database.GetPolicySettingAsync(
                "snapshot_monitoring_acknowledged", "false", cancellationToken);
            var enabled = await database.GetPolicySettingAsync(
                "snapshot_enabled", "false", cancellationToken);
            var snapshotsOn =
                string.Equals(acknowledged, "true", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase);

            var desktopExe = SnapshotPersistence.DesktopExePath;
            if (!File.Exists(desktopExe))
            {
                logger.LogWarning("Snapshot desktop agent is missing at {Path}. Checking for a software update.", desktopExe);
                await TryApplyLatestUpdateAsync(cancellationToken);
                return;
            }

            if (!snapshotsOn)
            {
                return;
            }

            SnapshotPersistence.EnsureLogonResume();

            if (SnapshotPersistence.IsDesktopAgentRunning())
            {
                return;
            }

            if (SnapshotPersistence.TryStartAgentNow(desktopExe))
            {
                logger.LogInformation("Snapshot agent was not running after boot/session change and has been restarted.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Snapshot agent supervisor failed to repair resume state.");
        }
    }

    private async Task TryApplyLatestUpdateAsync(CancellationToken cancellationToken)
    {
        var latest = await cloudRepository.GetLatestSoftwareUpdateAsync(Environment.MachineName, cancellationToken);
        if (latest is null)
        {
            return;
        }

        logger.LogInformation("Applying software update v{Version} because a local tool was missing or not running.", latest.Version);
        await softwareUpdater.ApplyUpdateAsync(latest, cancellationToken);
    }
}
