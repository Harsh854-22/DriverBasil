using SecureDeviceControl.Domain.Activity;

namespace SecureDeviceControl.Infrastructure.Persistence;

public interface ICloudRepository
{
    Task EnsureSchemaAsync(CancellationToken cancellationToken);

    Task RegisterDeviceAsync(
        string emailId,
        string machineName,
        CancellationToken cancellationToken);

    Task UploadActivityLogsAsync(
        IReadOnlyList<ActivityLogEntry> logs,
        CancellationToken cancellationToken);

    Task<CloudDevicePolicy?> GetDevicePolicyAsync(
        string emailId,
        CancellationToken cancellationToken);
}
