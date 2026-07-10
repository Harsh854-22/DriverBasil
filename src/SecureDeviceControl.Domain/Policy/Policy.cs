namespace SecureDeviceControl.Domain.Policy;

public sealed record Policy(
    PolicyMode Mode,
    TimeSpan SessionTimeout,
    TimeSpan DefaultUnlockDuration)
{
    public static Policy Default { get; } = new(
        PolicyMode.BlockUnknownRemovableStorage,
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(15));
}
