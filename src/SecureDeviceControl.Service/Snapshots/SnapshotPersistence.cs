using System.Diagnostics;
using System.Text;
using System.Xml.Linq;
using Microsoft.Win32;
using SecureDeviceControl.Shared;

namespace SecureDeviceControl.Service.Snapshots;

internal static class SnapshotPersistence
{
    public const string TaskName = "SecureDeviceControlSnapshots";
    public const string RunValueName = "SecureDeviceControlDesktop";
    public const string LegacyRunValueName = "SecureDeviceControlSnapshots";

    public static string DesktopExePath =>
        Path.Combine(AppContext.BaseDirectory, "SecureDeviceControl.Desktop.exe");

    public static void EnsureLogonResume(string? desktopExePath = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var exe = desktopExePath ?? DesktopExePath;
        if (!File.Exists(exe))
        {
            return;
        }

        try
        {
            RegisterMachineRunKey(exe);
        }
        catch
        {
            // HKLM write can fail if the service is not LocalSystem. Logon task remains the primary resume path.
        }

        try
        {
            RegisterLogonTask(exe);
        }
        catch
        {
            // Task Scheduler can be temporarily unavailable during boot; the supervisor retries.
        }
    }

    public static void RemoveLogonResume()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            key?.DeleteValue(RunValueName, throwOnMissingValue: false);
            key?.DeleteValue(LegacyRunValueName, throwOnMissingValue: false);
        }
        catch
        {
        }

        try
        {
            RunHidden("schtasks.exe", $"/Delete /TN \"{TaskName}\" /F");
        }
        catch
        {
        }
    }

    public static bool IsDesktopAgentRunning()
    {
        try
        {
            return Process.GetProcessesByName("SecureDeviceControl.Desktop").Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryStartAgentNow(string? desktopExePath = null)
    {
        if (IsDesktopAgentRunning())
        {
            return true;
        }

        var exe = desktopExePath ?? DesktopExePath;
        if (!File.Exists(exe))
        {
            return false;
        }

        if (InteractiveProcessLauncher.TryStart(exe, "--snapshot-agent"))
        {
            return true;
        }

        return TryRunScheduledTask();
    }

    private static void RegisterMachineRunKey(string exe)
    {
        using var key = Registry.LocalMachine.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true)
            ?? throw new InvalidOperationException("Could not open the machine Run key.");
        key.SetValue(RunValueName, $"\"{exe}\" --snapshot-agent", RegistryValueKind.String);
    }

    private static void RegisterLogonTask(string exe)
    {
        var xmlPath = Path.Combine(Path.GetTempPath(), $"{TaskName}.xml");
        File.WriteAllText(xmlPath, "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n" + BuildTaskXml(exe), Encoding.Unicode);
        try
        {
            RunHidden("schtasks.exe", $"/Create /TN \"{TaskName}\" /XML \"{xmlPath}\" /F");
        }
        finally
        {
            try { File.Delete(xmlPath); } catch { }
        }
    }

    private static bool TryRunScheduledTask()
    {
        try
        {
            return RunHidden("schtasks.exe", $"/Run /TN \"{TaskName}\"");
        }
        catch
        {
            return false;
        }
    }

    private static string BuildTaskXml(string exe)
    {
        var ns = XNamespace.Get("http://schemas.microsoft.com/windows/2004/02/mit/task");
        var document = new XDocument(
            new XDeclaration("1.0", "UTF-16", null),
            new XElement(ns + "Task",
                new XAttribute("version", "1.4"),
                new XElement(ns + "RegistrationInfo",
                    new XElement(ns + "URI", $"\\{TaskName}"),
                    new XElement(ns + "Description", $"{ServiceIdentity.DisplayName} snapshot agent. Restarts after sign-in, unlock, and reboot.")),
                new XElement(ns + "Triggers",
                    new XElement(ns + "LogonTrigger",
                        new XElement(ns + "Enabled", "true"),
                        new XElement(ns + "Delay", "PT15S")),
                    SessionTrigger(ns, "ConsoleConnect"),
                    SessionTrigger(ns, "SessionUnlock")),
                new XElement(ns + "Principals",
                    new XElement(ns + "Principal",
                        new XAttribute("id", "Users"),
                        new XElement(ns + "GroupId", "S-1-5-32-545"),
                        new XElement(ns + "RunLevel", "LeastPrivilege"),
                        new XElement(ns + "LogonType", "InteractiveToken"))),
                new XElement(ns + "Settings",
                    new XElement(ns + "MultipleInstancesPolicy", "IgnoreNew"),
                    new XElement(ns + "DisallowStartIfOnBatteries", "false"),
                    new XElement(ns + "StopIfGoingOnBatteries", "false"),
                    new XElement(ns + "AllowHardTerminate", "false"),
                    new XElement(ns + "StartWhenAvailable", "true"),
                    new XElement(ns + "AllowStartOnDemand", "true"),
                    new XElement(ns + "Enabled", "true"),
                    new XElement(ns + "Hidden", "false"),
                    new XElement(ns + "RunOnlyIfIdle", "false"),
                    new XElement(ns + "WakeToRun", "false"),
                    new XElement(ns + "ExecutionTimeLimit", "PT0S"),
                    new XElement(ns + "Priority", "7"),
                    new XElement(ns + "RestartOnFailure",
                        new XElement(ns + "Interval", "PT1M"),
                        new XElement(ns + "Count", 3))),
                new XElement(ns + "Actions",
                    new XAttribute("Context", "Users"),
                    new XElement(ns + "Exec",
                        new XElement(ns + "Command", exe),
                        new XElement(ns + "Arguments", "--snapshot-agent"),
                        new XElement(ns + "WorkingDirectory", Path.GetDirectoryName(exe) ?? AppContext.BaseDirectory)))));

        return document.ToString();
    }

    private static XElement SessionTrigger(XNamespace ns, string stateChange)
    {
        return new XElement(ns + "SessionStateChangeTrigger",
            new XElement(ns + "Enabled", "true"),
            new XElement(ns + "StateChange", stateChange));
    }

    private static bool RunHidden(string fileName, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        });
        if (process is null)
        {
            return false;
        }

        process.WaitForExit(15_000);
        return process.ExitCode == 0;
    }
}
