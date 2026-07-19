namespace SecureDeviceControl.Infrastructure.Updates;

public sealed record SoftwareUpdateModel(
    long Id,
    string Version,
    string DownloadUrl,
    string Sha256Hash,
    bool Mandatory,
    string TargetMachine,
    DateTimeOffset ReleasedAt);
