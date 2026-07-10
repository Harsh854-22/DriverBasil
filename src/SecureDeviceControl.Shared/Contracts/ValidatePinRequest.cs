using SecureDeviceControl.Shared.Security;

namespace SecureDeviceControl.Shared.Contracts;

public sealed record ValidatePinRequest(
    PinPurpose Purpose,
    string Pin);
