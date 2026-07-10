namespace SecureDeviceControl.Shared.Contracts;

public sealed record InitializePinsRequest(
    string DeviceUnlockPin,
    string UninstallPin);
