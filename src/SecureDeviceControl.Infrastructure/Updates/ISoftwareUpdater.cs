namespace SecureDeviceControl.Infrastructure.Updates;

public interface ISoftwareUpdater
{
    Task<bool> ApplyUpdateAsync(
        SoftwareUpdateModel updateModel,
        CancellationToken cancellationToken);

    bool VerifySha256(string filePath, string expectedHash);
}
