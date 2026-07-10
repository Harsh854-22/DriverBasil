using SecureDeviceControl.Shared.Security;

namespace SecureDeviceControl.Service.Tests;

public sealed class PinAttemptLimiterTests
{
    [Fact]
    public void CanAttemptBlocksPurposeAfterFiveFailures()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-07-09T10:00:00Z"));
        var limiter = new PinAttemptLimiter(clock);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.True(limiter.CanAttempt(PinPurpose.DeviceUnlock, out _));
            limiter.RecordFailure(PinPurpose.DeviceUnlock);
        }

        Assert.False(limiter.CanAttempt(PinPurpose.DeviceUnlock, out var retryAfter));
        Assert.Equal(DateTimeOffset.Parse("2026-07-09T10:00:30Z"), retryAfter);
    }

    [Fact]
    public void CanAttemptAllowsPurposeAfterLockoutExpires()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-07-09T10:00:00Z"));
        var limiter = new PinAttemptLimiter(clock);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            limiter.RecordFailure(PinPurpose.Uninstall);
        }

        Assert.False(limiter.CanAttempt(PinPurpose.Uninstall, out _));

        clock.Advance(TimeSpan.FromSeconds(31));

        Assert.True(limiter.CanAttempt(PinPurpose.Uninstall, out _));
    }
}
