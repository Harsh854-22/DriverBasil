using System.Diagnostics;
using System.IO;
using System.ServiceProcess;

namespace SecureDeviceControl.Desktop;

public static class ServiceInstallerHelper
{
    private const string ServiceName = "Secure Device Control";

    public static bool IsServiceRunning()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var sc = new ServiceController(ServiceName);
                return sc.Status == ServiceControllerStatus.Running;
            }
        }
        catch
        {
            // Service not installed or query failed
        }
        return false;
    }

    public static bool EnsureServiceRunning()
    {
        if (IsServiceRunning())
        {
            return true;
        }

        // Find service executable
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var serviceExePath = Path.Combine(baseDir, "SecureDeviceControl.Service.exe");
        if (!File.Exists(serviceExePath))
        {
            serviceExePath = Path.Combine(baseDir, "Service", "SecureDeviceControl.Service.exe");
        }

        if (!File.Exists(serviceExePath))
        {
            return false;
        }

        // Standard Windows service creation & startup using cmd.exe with UAC elevation
        var cmdArguments = $"/c sc.exe create \"{ServiceName}\" binPath= \"\"{serviceExePath}\"\" start= auto & sc.exe config \"{ServiceName}\" obj= LocalSystem & sc.exe start \"{ServiceName}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = cmdArguments,
            Verb = "runas", // Pops standard Windows UAC Yes/No elevation prompt
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        try
        {
            using var proc = Process.Start(startInfo);
            proc?.WaitForExit(8_000);
            Thread.Sleep(1500); // Allow service time to initialize
        }
        catch
        {
            // User declined UAC prompt or process failed
        }

        return IsServiceRunning();
    }
}
