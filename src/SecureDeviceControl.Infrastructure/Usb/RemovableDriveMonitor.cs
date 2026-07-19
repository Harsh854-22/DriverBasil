using Microsoft.Extensions.Logging;

namespace SecureDeviceControl.Infrastructure.Usb;

public sealed class RemovableDriveMonitor : IRemovableDriveMonitor
{
    private readonly ILogger<RemovableDriveMonitor> logger;
    private readonly Dictionary<string, FileSystemWatcher> activeWatchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly object lockObj = new();
    private Action<string, long, string>? fileWrittenCallback;
    private bool isMonitoring = false;

    public RemovableDriveMonitor(ILogger<RemovableDriveMonitor> logger)
    {
        this.logger = logger;
    }

    public void StartMonitoring(Action<string, long, string> onFileWritten)
    {
        lock (lockObj)
        {
            fileWrittenCallback = onFileWritten;
            isMonitoring = true;
            RefreshWatchers();
        }
    }

    public void StopMonitoring()
    {
        lock (lockObj)
        {
            isMonitoring = false;
            fileWrittenCallback = null;
            foreach (var watcher in activeWatchers.Values)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            activeWatchers.Clear();
        }
    }

    private void RefreshWatchers()
    {
        if (!isMonitoring) return;

        try
        {
            var removableDrives = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Removable && d.IsReady)
                .ToList();

            foreach (var drive in removableDrives)
            {
                var driveRoot = drive.RootDirectory.FullName;
                if (!activeWatchers.ContainsKey(driveRoot))
                {
                    try
                    {
                        var watcher = new FileSystemWatcher(driveRoot)
                        {
                            IncludeSubdirectories = true,
                            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
                        };

                        watcher.Created += (s, e) => OnFileEvent(e.FullPath, e.Name, driveRoot);
                        watcher.Changed += (s, e) => OnFileEvent(e.FullPath, e.Name, driveRoot);
                        watcher.EnableRaisingEvents = true;

                        activeWatchers[driveRoot] = watcher;
                        logger.LogInformation("Attached real-time file copy monitor to removable volume '{Drive}'", driveRoot);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Could not attach FileSystemWatcher to volume '{Drive}'", driveRoot);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error checking removable drives.");
        }
    }

    private void OnFileEvent(string fullPath, string? relativeName, string driveRoot)
    {
        if (string.IsNullOrWhiteSpace(fullPath)) return;

        try
        {
            var info = new FileInfo(fullPath);
            if (!info.Exists) return;

            // Filter out system hidden files
            if (info.Attributes.HasFlag(FileAttributes.Hidden) || info.Name.StartsWith("~$")) return;

            var size = info.Length;
            fileWrittenCallback?.Invoke(info.Name, size, driveRoot);
        }
        catch
        {
            // Ignore access errors on transient files
        }
    }

    public void Dispose()
    {
        StopMonitoring();
    }
}
