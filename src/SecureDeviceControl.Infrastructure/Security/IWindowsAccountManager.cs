namespace SecureDeviceControl.Infrastructure.Security;

public interface IWindowsAccountManager
{
    Task<bool> ChangeLocalUserPasswordAsync(
        string username,
        string newPassword,
        CancellationToken cancellationToken);
}
