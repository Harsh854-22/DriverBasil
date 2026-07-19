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

    Task<IReadOnlyList<WindowsPasswordCommand>> GetPendingWindowsPasswordCommandsAsync(
        string emailId,
        string machineName,
        CancellationToken cancellationToken);

    Task UpdateWindowsPasswordCommandStatusAsync(
        long commandId,
        string status,
        string? errorMessage,
        CancellationToken cancellationToken);

    Task<SecureDeviceControl.Infrastructure.Updates.SoftwareUpdateModel?> GetLatestSoftwareUpdateAsync(
        string machineName,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RemoteCommand>> GetPendingRemoteCommandsAsync(
        string emailId,
        string machineName,
        CancellationToken cancellationToken);

    Task UpdateRemoteCommandStatusAsync(
        long commandId,
        string status,
        string? errorMessage,
        CancellationToken cancellationToken);
}
