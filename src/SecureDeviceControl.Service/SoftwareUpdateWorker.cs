using System.Reflection;
using SecureDeviceControl.Domain.Activity;
using SecureDeviceControl.Infrastructure.Persistence;
using SecureDeviceControl.Infrastructure.Updates;

namespace SecureDeviceControl.Service;

public sealed class SoftwareUpdateWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);

    private readonly DeviceControlDatabase localDatabase;
    private readonly ICloudRepository cloudRepository;
    private readonly ISoftwareUpdater softwareUpdater;
    private readonly ILogger<SoftwareUpdateWorker> logger;

    public SoftwareUpdateWorker(
        DeviceControlDatabase localDatabase,
        ICloudRepository cloudRepository,
        ISoftwareUpdater softwareUpdater,
        ILogger<SoftwareUpdateWorker> logger)
    {
        this.localDatabase = localDatabase;
        this.cloudRepository = cloudRepository;
        this.softwareUpdater = softwareUpdater;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Software update worker started.");

        // Check for updates immediately on startup
        try
        {
            await CheckForUpdatesAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Initial update check failed.");
        }

        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CheckForUpdatesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Software update polling check failed.");
            }
        }
    }

    private async Task CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        var machineName = Environment.MachineName;
        var latestRelease = await cloudRepository.GetLatestSoftwareUpdateAsync(machineName, cancellationToken);

        if (latestRelease is null)
        {
            return;
        }

        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
        if (!Version.TryParse(latestRelease.Version, out var remoteVersion))
        {
            logger.LogWarning("Remote update version string '{Version}' could not be parsed.", latestRelease.Version);
            return;
        }

        if (remoteVersion > currentVersion)
        {
            logger.LogInformation("New software update release available: v{RemoteVersion} (Current: v{CurrentVersion}). Target: {Target}",
                remoteVersion, currentVersion, latestRelease.TargetMachine);

            var success = await softwareUpdater.ApplyUpdateAsync(latestRelease, cancellationToken);
            if (success)
            {
                await localDatabase.AppendActivityLogAsync(
                    ActivityLogEventType.PolicyEvaluated,
                    $"[SOFTWARE UPDATE] Triggered silent upgrade to v{remoteVersion}.",
                    cancellationToken);
            }
        }
    }
}
