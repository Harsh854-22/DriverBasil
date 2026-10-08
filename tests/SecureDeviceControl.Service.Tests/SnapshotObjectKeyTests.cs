using SecureDeviceControl.Shared.Snapshots;

namespace SecureDeviceControl.Service.Tests;

public sealed class SnapshotObjectKeyTests
{
    [Fact]
    public void SanitizeMachineName_KeepsWindowsPcNames()
    {
        Assert.Equal("OFFICE-PC-01", SnapshotObjectKey.SanitizeMachineName("OFFICE-PC-01"));
        Assert.Equal("DESKTOP-ABC123", SnapshotObjectKey.SanitizeMachineName("DESKTOP-ABC123"));
    }

    [Fact]
    public void SanitizeMachineName_ReplacesUnsafeCharacters()
    {
        Assert.Equal("PC-NAME", SnapshotObjectKey.SanitizeMachineName("PC/NAME"));
        Assert.Equal("UNKNOWN-PC", SnapshotObjectKey.SanitizeMachineName("   "));
    }

    [Fact]
    public void Build_UsesPcNameNotHash()
    {
        var capturedAt = new DateTimeOffset(2026, 9, 16, 6, 15, 30, 123, TimeSpan.Zero);
        var key = SnapshotObjectKey.Build("OFFICE-PC-01", capturedAt);

        Assert.Equal("OFFICE-PC-01/2026-09-16/20260916-061530-123.jpg", key);
        Assert.DoesNotContain("abc", key, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DayPrefixes_IncludeFlatDateAndNestedDate()
    {
        var prefixes = SnapshotObjectKey.DayPrefixes("OFFICE-PC-01", "employee@company.com", new DateTime(2026, 9, 16));
        Assert.Contains("OFFICE-PC-01/2026-09-16/", prefixes);
        Assert.Contains("OFFICE-PC-01/2026/09/16/", prefixes);
    }

    [Fact]
    public void LegacyDeviceHash_StaysStableForOldBucketFolders()
    {
        var hash = SnapshotObjectKey.LegacyDeviceHash("OFFICE-PC-01", "employee@company.com");
        Assert.Equal(16, hash.Length);
        Assert.Equal(hash, SnapshotObjectKey.LegacyDeviceHash("OFFICE-PC-01", "employee@company.com"));
    }
}
