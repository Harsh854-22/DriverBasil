using SecureDeviceControl.Shared.Security;

namespace SecureDeviceControl.Service.Tests;

public sealed class SecurityStressTests
{
    [Fact]
    public void SessionManagerRejectsLargeVolumeOfInvalidTokens()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-07-09T10:00:00Z"));
        var sessionManager = new SessionManager(clock);
        var rejectedCount = 0;

        for (var index = 0; index < 50_000; index++)
        {
            if (!sessionManager.IsValid(Convert.ToBase64String(Guid.NewGuid().ToByteArray()), PinPurpose.DeviceUnlock))
            {
                rejectedCount++;
            }
        }

        Assert.Equal(50_000, rejectedCount);
    }

    [Fact]
    public void PinAttemptLimiterHandlesRepeatedInvalidAttemptsWithoutFailingOpen()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-07-09T10:00:00Z"));
        var limiter = new PinAttemptLimiter(clock);
        var blockedCount = 0;

        for (var index = 0; index < 10_000; index++)
        {
            if (!limiter.CanAttempt(PinPurpose.DeviceUnlock, out _))
            {
                blockedCount++;
                continue;
            }

            limiter.RecordFailure(PinPurpose.DeviceUnlock);
        }

        Assert.True(blockedCount > 9_900);
        Assert.False(limiter.CanAttempt(PinPurpose.DeviceUnlock, out _));
    }
}
