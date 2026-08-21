using System.Security.Cryptography;
using System.Text;

namespace SecureDeviceControl.Dashboard.Cloud;

public sealed record DeviceInfo(string Email, string MachineName, string DeviceHash, DateTimeOffset UpdatedAt)
{
    public static string ComputeDeviceHash(string machineName, string email)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{machineName}|{email}")))[..16].ToLowerInvariant();
    }

    public string DisplayName => $"{MachineName} ({Email})";
}
