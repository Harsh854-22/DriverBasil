using System.Globalization;

namespace SecureDeviceControl.SnapshotPortal.Storage;

public sealed record SnapshotFrame(string ObjectKey, string DeviceFolder, DateTimeOffset CapturedAtUtc);

public static class SnapshotCatalog
{
    public static SnapshotFrame? TryParse(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey) || objectKey.Length > 240 || objectKey.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        if (objectKey.Contains('\\') || objectKey.Contains('?') || objectKey.Contains('#') || objectKey.Contains('%'))
        {
            return null;
        }

        var parts = objectKey.Split('/');
        if (parts.Length is not (3 or 5))
        {
            return null;
        }

        if (parts.Any(part => part.Length is < 1 or > 80 || !IsSafePart(part)))
        {
            return null;
        }

        var fileNamePart = parts[^1];
        if (!fileNamePart.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

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

        return new SnapshotFrame(objectKey, parts[0], capturedAt);
    }

    private static bool IsSafePart(string part)
    {
        foreach (var character in part)
        {
            if (character is not ((>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-' or '_' or '.'))
            {
                return false;
            }
        }

        return true;
    }
}
