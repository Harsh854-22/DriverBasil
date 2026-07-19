namespace SecureDeviceControl.Infrastructure.Usb;

public interface IRemovableDriveMonitor : IDisposable
{
    void StartMonitoring(Action<string, long, string> onFileWritten);
    void StopMonitoring();
}
