namespace SecureDeviceControl.Shared.Contracts;

public sealed record SetDeviceClassLockRequest(
    DeviceClass DeviceClass,
    bool Locked);
