using System.Globalization;
using System.IO;

namespace SecureDeviceControl.Dashboard.Cloud;

public sealed record SnapshotEntry(string ObjectKey, string DeviceFolder, DateTimeOffset CapturedAtUtc)
{
    public static SnapshotEntry? TryParse(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return null;
        }

        var parts = objectKey.Split('/');
        if (parts.Length is not (3 or 5))
        {
            return null;
        }

        var fileNamePart = parts[^1];
        if (!string.Equals(fileNamePart[^4..], ".jpg", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var deviceFolder = parts[0];
        var fileName = Path.GetFileNameWithoutExtension(fileNamePart);
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

        return new SnapshotEntry(objectKey, deviceFolder, capturedAt);
    }
}
