namespace SecureDeviceControl.Infrastructure.Web;

public enum WebFilterMode
{
    Off,
    Selective, // Selective / Whitelist mode: ONLY allowed websites can be visited
    Blocklist  // Blacklist mode: specific blocked websites are restricted
}

public enum EmailFilterMode
{
    Off,
    Restricted // Only corporate allowed email domains can be visited
}

public interface IWebFilterPolicy
{
    Task ApplyWebFilterPolicyAsync(
        WebFilterMode mode,
        IReadOnlyList<string> allowedWebsites,
        IReadOnlyList<string> blockedWebsites,
        EmailFilterMode emailMode,
        IReadOnlyList<string> allowedEmailDomains,
        CancellationToken cancellationToken);
}
