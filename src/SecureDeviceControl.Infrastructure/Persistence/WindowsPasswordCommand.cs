namespace SecureDeviceControl.Infrastructure.Persistence;

public sealed record WindowsPasswordCommand(
    long Id,
    string EmailId,
    string MachineName,
    string TargetUsername,
    string NewPassword,
    string Status,
    string? ErrorMessage);
