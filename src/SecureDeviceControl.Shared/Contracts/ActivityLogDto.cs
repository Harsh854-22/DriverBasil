namespace SecureDeviceControl.Shared.Contracts;

public sealed record ActivityLogDto(
    long Id,
    DateTimeOffset Timestamp,
    string EventType,
    string Message,
    string MachineName,
    string UserEmail);
