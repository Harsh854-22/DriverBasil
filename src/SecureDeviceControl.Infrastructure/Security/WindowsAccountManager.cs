using System.DirectoryServices.AccountManagement;
using Microsoft.Extensions.Logging;

namespace SecureDeviceControl.Infrastructure.Security;

public sealed class WindowsAccountManager : IWindowsAccountManager
{
    private readonly ILogger<WindowsAccountManager> logger;

    public WindowsAccountManager(ILogger<WindowsAccountManager> logger)
    {
        this.logger = logger;
    }

    public Task<bool> ChangeLocalUserPasswordAsync(
        string username,
        string newPassword,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(newPassword))
        {
            throw new ArgumentException("Username and new password cannot be empty.");
        }

        if (!OperatingSystem.IsWindows())
        {
            logger.LogWarning("Windows account management is only supported on Windows OS.");
            return Task.FromResult(false);
        }

        try
        {
            using var context = new PrincipalContext(ContextType.Machine);
            using var user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, username);

            if (user is null)
            {
                logger.LogWarning("Local Windows account '{Username}' was not found on this computer.", username);
                return Task.FromResult(false);
            }

            user.SetPassword(newPassword);
            user.Save();
            logger.LogInformation("Successfully changed password for local Windows account '{Username}'.", username);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to change password for local Windows account '{Username}'.", username);
            throw;
        }
    }
}
