using System.Globalization;
using System.IO;

namespace SecureDeviceControl.Dashboard.Cloud;

public sealed record SnapshotEntry(string ObjectKey, string DeviceHash, DateTimeOffset CapturedAtUtc)
{
    public static SnapshotEntry? TryParse(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return null;
        }

        var parts = objectKey.Split('/');
        if (parts.Length != 5 || !string.Equals(parts[4][^4..], ".jpg", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var deviceHash = parts[0];
        var fileName = Path.GetFileNameWithoutExtension(parts[4]);
        var timePart = fileName.Length >= 19 ? fileName[..19] : fileName;
        if (!DateTimeOffset.TryParseExact(
                timePart,
                "yyyyMMdd-HHmmss-fff",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var capturedAt))
        {
            return null;
        }

        return new SnapshotEntry(objectKey, deviceHash, capturedAt);
    }
}
