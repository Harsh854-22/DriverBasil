namespace SecureDeviceControl.Domain.Devices;

public sealed record TrustedDevice(
    string StableId,
    string FriendlyName,
    DateTimeOffset TrustedAt);
