namespace SecureDeviceControl.Shared.Contracts;

public sealed record InitializePinsRequest(
    string UserEmail,
    string DeviceUnlockPin,
    string UninstallPin);
