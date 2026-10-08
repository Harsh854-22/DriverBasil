using Microsoft.Win32;

namespace SecureDeviceControl.Desktop;

public static class SnapshotStartupRegistrar
{
    private const string RunKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string ValueName = "SecureDeviceControlDesktop";
    private const string LegacyValueName = "SecureDeviceControlSnapshots";

    public static void EnableForCurrentUser()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return;
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                return;
            }

            key.SetValue(ValueName, $"\"{executablePath}\" --snapshot-agent", RegistryValueKind.String);
            key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
        }
        catch
        {
            // Sign-in resume is also registered machine-wide by the Windows service.
        }
    }
}
