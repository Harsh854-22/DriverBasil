using SecureDeviceControl.Shared.Snapshots;

namespace SecureDeviceControl.Dashboard.Cloud;

public sealed record DeviceInfo(string Email, string MachineName, string StoragePrefix, DateTimeOffset UpdatedAt)
{
    public static string ComputeLegacyDeviceHash(string machineName, string email)
    {
        return SnapshotObjectKey.LegacyDeviceHash(machineName, email);
    }

    public string DisplayName => $"{MachineName} ({Email})";
}
