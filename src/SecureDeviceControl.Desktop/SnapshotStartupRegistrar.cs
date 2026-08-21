using Microsoft.Win32;

namespace SecureDeviceControl.Desktop;

public static class SnapshotStartupRegistrar
{
    private const string RunKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string ValueName = "SecureDeviceControlDesktop";

    public static void EnableForCurrentUser()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException("The application path is unavailable.");
        }

        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Windows could not enable snapshot monitoring at sign-in.");
        key.SetValue(ValueName, $"\"{executablePath}\" --snapshot-agent", RegistryValueKind.String);
    }
}
