namespace SecureDeviceControl.Infrastructure.Persistence;

public sealed record RemoteCommand(
    long Id,
    string EmailId,
    string MachineName,
    string Command,
    string Payload,
    string Status,
    string? ErrorMessage);
