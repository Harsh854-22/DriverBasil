using System.Security.Cryptography;
using System.Text;

namespace SecureDeviceControl.Shared.Snapshots;

public static class SnapshotObjectKey
{
    public static string SanitizeMachineName(string? machineName)
    {
        if (string.IsNullOrWhiteSpace(machineName))
        {
            return "UNKNOWN-PC";
        }

        var trimmed = machineName.Trim();
        var buffer = new char[trimmed.Length];
        var length = 0;
        foreach (var character in trimmed)
        {
            buffer[length++] = char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'
                ? character
                : '-';
        }

        var sanitized = new string(buffer, 0, length).Trim('-', '.', '_');
        if (string.IsNullOrEmpty(sanitized))
        {
            return "UNKNOWN-PC";
        }

        return sanitized.Length <= 64
            ? sanitized
            : sanitized[..64].Trim('-', '.', '_');
    }

    public static string Build(string machineName, DateTimeOffset capturedAtUtc)
    {
        var folder = SanitizeMachineName(machineName);
        var utc = capturedAtUtc.ToUniversalTime();
        return $"{folder}/{utc:yyyy-MM-dd}/{utc:yyyyMMdd-HHmmss-fff}.jpg";
    }

    public static IReadOnlyList<string> DayPrefixes(string machineName, string email, DateTime day)
    {
        var folder = SanitizeMachineName(machineName);
        var hash = LegacyDeviceHash(machineName, email);
        var nested = $"{day:yyyy}/{day:MM}/{day:dd}/";
        var flat = $"{day:yyyy-MM-dd}/";
        return new[]
        {
            folder + "/" + flat,
            folder + "/" + nested,
            hash + "/" + flat,
            hash + "/" + nested
        };
    }

    public static string LegacyDeviceHash(string machineName, string email)
    {
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{machineName}|{email}")))[..16].ToLowerInvariant();
    }
}
