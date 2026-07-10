namespace SecureDeviceControl.Shared.Contracts;

public sealed record UninstallAuthorizationResult(
    string AuthorizationToken,
    DateTimeOffset ExpiresAt);
