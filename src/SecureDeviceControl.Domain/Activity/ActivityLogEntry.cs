namespace SecureDeviceControl.Domain.Activity;

public sealed record ActivityLogEntry(
    long Id,
    DateTimeOffset Timestamp,
    ActivityLogEventType EventType,
    string Message,
    string MachineName = "",
    string UserEmail = "");
