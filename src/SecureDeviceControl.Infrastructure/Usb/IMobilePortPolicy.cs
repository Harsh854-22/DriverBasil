namespace SecureDeviceControl.Infrastructure.Usb;

public interface IMobilePortPolicy
{
    Task<bool> IsMobilePortLockedAsync(CancellationToken cancellationToken);

    Task SetMobilePortLockedAsync(bool locked, CancellationToken cancellationToken);
}
