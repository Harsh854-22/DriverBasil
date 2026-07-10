using SecureDeviceControl.Shared.Security;

namespace SecureDeviceControl.Service;

public sealed class PinAttemptLimiter
{
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromSeconds(30);
    private const int MaxFailedAttempts = 5;

    private readonly TimeProvider timeProvider;
    private readonly Dictionary<PinPurpose, AttemptState> attempts = new();

    public PinAttemptLimiter(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
    }

    public bool CanAttempt(PinPurpose purpose, out DateTimeOffset? retryAfter)
    {
        lock (attempts)
        {
            retryAfter = null;
            if (!attempts.TryGetValue(purpose, out var state) || state.BlockedUntil is null)
            {
                return true;
            }

            if (state.BlockedUntil <= timeProvider.GetUtcNow())
            {
                state.BlockedUntil = null;
                state.FailedAttempts = 0;
                return true;
            }

            retryAfter = state.BlockedUntil;
            return false;
        }
    }

    public void RecordFailure(PinPurpose purpose)
    {
        lock (attempts)
        {
            if (!attempts.TryGetValue(purpose, out var state))
            {
                state = new AttemptState();
                attempts[purpose] = state;
            }

            state.FailedAttempts++;
            if (state.FailedAttempts >= MaxFailedAttempts)
            {
                state.BlockedUntil = timeProvider.GetUtcNow().Add(LockoutDuration);
            }
        }
    }

    public void RecordSuccess(PinPurpose purpose)
    {
        lock (attempts)
        {
            attempts.Remove(purpose);
        }
    }

    private sealed class AttemptState
    {
        public int FailedAttempts { get; set; }

        public DateTimeOffset? BlockedUntil { get; set; }
    }
}
