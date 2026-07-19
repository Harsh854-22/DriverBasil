namespace SecureDeviceControl.Infrastructure.Persistence;

public sealed record CloudDevicePolicy(
    string EmailId,
    string MachineName,
    string WebFilterMode,
    string AllowedWebsites,
    string BlockedWebsites,
    string EmailFilterMode,
    string AllowedEmailDomains);
