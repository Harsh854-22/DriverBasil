namespace SecureDeviceControl.Infrastructure.Paths;

public sealed class ProgramDataPaths
{
    public ProgramDataPaths(string? baseDirectory = null)
    {
        BaseDirectory = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "SecureDeviceControl");
    }

    public string BaseDirectory { get; }

    public string DatabasePath => Path.Combine(BaseDirectory, "secure-device-control.db");

    public string UninstallAuthorizationPath => Path.Combine(BaseDirectory, "uninstall-authorization.token");

    public void EnsureBaseDirectory()
    {
        Directory.CreateDirectory(BaseDirectory);
    }
}
