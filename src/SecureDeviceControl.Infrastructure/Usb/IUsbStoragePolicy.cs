namespace SecureDeviceControl.Infrastructure.Usb;

public interface IUsbStoragePolicy
{
    Task<bool> IsUsbStorageLockedAsync(CancellationToken cancellationToken);

    Task SetUsbStorageLockedAsync(bool locked, CancellationToken cancellationToken);
}
