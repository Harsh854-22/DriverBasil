namespace SecureDeviceControl.Infrastructure.Vpn;

public enum VpnFilterMode
{
    Off,
    Blocked
}

public interface IVpnFilterPolicy
{
    Task ApplyVpnFilterPolicyAsync(
        VpnFilterMode mode,
        CancellationToken cancellationToken);
}
