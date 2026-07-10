namespace SecureDeviceControl.Domain.Policy;

public sealed record UnlockTimer(DateTimeOffset? ExpiresAt)
{
    public static UnlockTimer Inactive { get; } = new((DateTimeOffset?)null);

    public bool IsActiveAt(DateTimeOffset now)
    {
        return ExpiresAt is not null && ExpiresAt > now;
    }
}
