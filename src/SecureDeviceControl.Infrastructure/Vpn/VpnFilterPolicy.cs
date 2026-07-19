using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace SecureDeviceControl.Infrastructure.Vpn;

public sealed class VpnFilterPolicy : IVpnFilterPolicy
{
    private static readonly string[] ForbiddenVpnProcesses =
    {
        "NordVPN", "NordVPN.exe",
        "ExpressVPN", "expressvpn.exe",
        "ProtonVPN", "ProtonVPN.exe",
        "Windscribe", "windscribe.exe",
        "TunnelBear", "TunnelBear.exe",
        "openvpn", "openvpn.exe",
        "wireguard", "wireguard.exe",
        "HotspotShield", "hotspotshield.exe",
        "CyberGhost", "CyberGhost.exe",
        "Surfshark", "surfshark.exe"
    };

    public static readonly string[] StandardBrowserVpnProxyDomains =
    {
        "touchvpn.net", "www.touchvpn.net",
        "hola.org", "www.hola.org",
        "zenmate.com", "www.zenmate.com",
        "browsec.com", "www.browsec.com",
        "urban-vpn.com", "www.urban-vpn.com",
        "setupvpn.com", "www.setupvpn.com",
        "betternet.co", "www.betternet.co",
        "cyberghostvpn.com", "www.cyberghostvpn.com",
        "expressvpn.com", "www.expressvpn.com",
        "nordvpn.com", "www.nordvpn.com",
        "surfshark.com", "www.surfshark.com",
        "mullvad.net", "www.mullvad.net",
        "protonvpn.com", "www.protonvpn.com",
        "windscribe.com", "www.windscribe.com"
    };

    private readonly ILogger<VpnFilterPolicy> logger;

    public VpnFilterPolicy(ILogger<VpnFilterPolicy> logger)
    {
        this.logger = logger;
    }

    public Task ApplyVpnFilterPolicyAsync(
        VpnFilterMode mode,
        CancellationToken cancellationToken)
    {
        if (mode == VpnFilterMode.Off)
        {
            return Task.CompletedTask;
        }

        try
        {
            foreach (var processName in ForbiddenVpnProcesses)
            {
                var cleanName = processName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
                var activeProcesses = Process.GetProcessesByName(cleanName);

                foreach (var proc in activeProcesses)
                {
                    try
                    {
                        proc.Kill(entireProcessTree: true);
                        logger.LogWarning("Terminated unauthorized VPN process '{ProcessName}' (PID: {Pid}).", cleanName, proc.Id);
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Could not kill VPN process '{ProcessName}'", cleanName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while enforcing VPN process termination policy.");
        }

        return Task.CompletedTask;
    }
}
