using SecureDeviceControl.Shared.Security;

namespace SecureDeviceControl.Service.Tests;

public sealed class SessionManagerTests
{
    [Fact]
    public void IsValidRejectsSessionCreatedForDifferentPurpose()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-07-09T10:00:00Z"));
        var sessionManager = new SessionManager(clock);
        var uninstallSession = sessionManager.Create(PinPurpose.Uninstall);

        Assert.False(sessionManager.IsValid(uninstallSession.Token, PinPurpose.DeviceUnlock));
        Assert.True(sessionManager.IsValid(uninstallSession.Token, PinPurpose.Uninstall));
    }

    [Fact]
    public void IsValidRejectsExpiredSession()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-07-09T10:00:00Z"));
        var sessionManager = new SessionManager(clock);
        var session = sessionManager.Create(PinPurpose.DeviceUnlock);

        clock.Advance(TimeSpan.FromMinutes(16));

        Assert.False(sessionManager.IsValid(session.Token, PinPurpose.DeviceUnlock));
    }
}
