namespace SecureDeviceControl.Infrastructure.Persistence;

public interface ISnapshotStorage
{
    Task UploadAsync(string objectKey, byte[] imageBytes, CancellationToken cancellationToken);
}
