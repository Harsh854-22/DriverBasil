using SecureDeviceControl.SnapshotPortal.Security;
using SecureDeviceControl.SnapshotPortal.Storage;

namespace SecureDeviceControl.SnapshotPortal.Tests;

public sealed class SnapshotCatalogTests
{
    [Fact]
    public void Accepts_flat_and_nested_snapshot_keys()
    {
        var flat = SnapshotCatalog.TryParse("FRONT-DESK/2026-10-08/20261008-093012-123.jpg");
        var nested = SnapshotCatalog.TryParse("FRONT-DESK/2026/10/08/20261008-093012-123.jpg");

        Assert.NotNull(flat);
        Assert.NotNull(nested);
        Assert.Equal("FRONT-DESK", flat!.DeviceFolder);
    }

    [Theory]
    [InlineData("../secret.jpg")]
    [InlineData("PC/2026-10-08/../../etc/passwd")]
    [InlineData("PC/2026-10-08/note.txt")]
    [InlineData("PC/2026-10-08/not-a-time.jpg")]
    [InlineData("bad key/2026-10-08/20261008-093012-123.jpg")]
    public void Rejects_keys_that_are_not_snapshot_frames(string key)
    {
        Assert.Null(SnapshotCatalog.TryParse(key));
    }
}

public sealed class PortalSealTests
{
    [Fact]
    public void Rejects_a_wrong_passphrase()
    {
        Assert.False(PortalSeal.Verify("wrong-passphrase"));
        Assert.False(PortalSeal.Verify(""));
    }

    [Fact]
    public void Accepts_the_operator_passphrase_when_the_test_supplies_it()
    {
        var password = Environment.GetEnvironmentVariable("SNAPSHOT_PORTAL_PASSWORD");
        if (string.IsNullOrEmpty(password))
        {
            return;
        }

        Assert.True(PortalSeal.Verify(password));
    }
}

public sealed class SessionStoreTests
{
    [Fact]
    public void Drops_a_session_presented_by_a_different_browser()
    {
        var store = new SessionStore();
        var (sessionId, _) = store.Issue("BrowserA");

        Assert.NotNull(store.Find(sessionId, "BrowserA"));
        Assert.Null(store.Find(sessionId, "BrowserB"));
        Assert.Null(store.Find(sessionId + "tampered", "BrowserA"));
    }

    [Fact]
    public void Locks_a_client_after_repeated_failures()
    {
        var throttle = new LoginThrottle();
        var now = DateTimeOffset.Parse("2026-10-08T00:00:00Z");
        for (var attempt = 0; attempt < 5; attempt++)
        {
            throttle.RecordFailure("127.0.0.1", now);
        }

        Assert.True(throttle.IsLocked("127.0.0.1", now.AddMinutes(1)));
        throttle.Reset("127.0.0.1");
        Assert.False(throttle.IsLocked("127.0.0.1", now.AddMinutes(1)));
    }
}
