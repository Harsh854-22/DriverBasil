using System.Diagnostics;
using System.IO;
using System.ServiceProcess;

namespace SecureDeviceControl.Desktop;

public static class ServiceInstallerHelper
{
    private const string ServiceName = "Secure Device Control";

    public static void EnsureServiceRunning()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var sc = new ServiceController(ServiceName);
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    return;
                }
            }
        }
        catch
        {
            // Service not installed or status check failed
        }

        // Auto-install and start background service with elevation
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var serviceExePath = Path.Combine(baseDir, "SecureDeviceControl.Service.exe");
        if (!File.Exists(serviceExePath))
        {
            serviceExePath = Path.Combine(baseDir, "Service", "SecureDeviceControl.Service.exe");
        }

        if (!File.Exists(serviceExePath))
        {
            return;
        }

        var psCommand = $"sc.exe create '{ServiceName}' binPath= '{serviceExePath}' start= auto obj= LocalSystem; sc.exe config '{ServiceName}' obj= LocalSystem; sc.exe start '{ServiceName}'";

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCommand}\"",
            Verb = "runas", // Pops standard Windows UAC Yes/No prompt
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        try
        {
            using var proc = Process.Start(startInfo);
            proc?.WaitForExit(8_000);
            Thread.Sleep(1500); // Allow service time to transition to RUNNING
        }
        catch
        {
            // User declined UAC prompt or process failed
        }
    }
}
