namespace SecureDeviceControl.Shared.Contracts;

public sealed record ServiceStatusDto(
    bool IsInitialized,
    bool IsUsbStorageLocked,
    bool IsUnlockTimerActive,
    DateTimeOffset? UnlockExpiresAt);
