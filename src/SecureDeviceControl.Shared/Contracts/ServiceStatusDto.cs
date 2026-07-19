namespace SecureDeviceControl.Shared.Contracts;

public sealed record ServiceStatusDto(
    bool IsInitialized,
    bool IsUsbStorageLocked,
    bool IsMobilePortLocked,
    bool IsUnlockTimerActive,
    DateTimeOffset? UnlockExpiresAt,
    string? UserEmail = null,
    string? MachineName = null,
    string? WebFilterMode = "OFF",
    string? EmailFilterMode = "OFF");
