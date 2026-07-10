using SecureDeviceControl.Shared.Security;

namespace SecureDeviceControl.Shared.Contracts;

public sealed record ValidatePinResult(
    PinPurpose Purpose,
    string SessionToken,
    DateTimeOffset ExpiresAt);
