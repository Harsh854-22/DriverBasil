namespace SecureDeviceControl.Service.Tests;

internal sealed class MutableTimeProvider : TimeProvider
{
    private DateTimeOffset utcNow;

    public MutableTimeProvider(DateTimeOffset utcNow)
    {
        this.utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow()
    {
        return utcNow;
    }

    public void Advance(TimeSpan value)
    {
        utcNow = utcNow.Add(value);
    }
}
