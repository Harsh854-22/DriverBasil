using Microsoft.Data.Sqlite;
using SecureDeviceControl.Domain.Activity;
using SecureDeviceControl.Domain.Security;
using SecureDeviceControl.Infrastructure.Paths;
using SecureDeviceControl.Infrastructure.Persistence;
using SecureDeviceControl.Infrastructure.Security;
using SecureDeviceControl.Shared.Security;

namespace SecureDeviceControl.Service.Provisioning;

public sealed class ProvisionValidationException : Exception
{
    public ProvisionValidationException(string message)
        : base(message)
    {
    }
}

public enum ProvisionOutcome
{
    ProvisionedFresh,
    UpdatedExisting,
    RecoveredCorruptDatabase
}

public sealed record ProvisionRequest(
    string? UserEmail,
    string DeviceUnlockPin,
    string UninstallPin,
    bool AcknowledgeSnapshots);

public sealed record ProvisionResult(
    ProvisionOutcome Outcome,
    string UserEmail,
    bool DatabaseWasRecovered,
    string? RecoveryDirectory);

/// <summary>
/// Zero-touch provisioning used by the installer (<c>install-service.ps1 -AutoProvision</c>)
/// and by the <c>provision</c> CLI verb. It writes the exact same records as the desktop
/// registration flow (Argon2id PIN hashes, DPAPI-protected credentials, policy settings)
/// without requiring the desktop UI. It never touches the hardware lock settings
/// (<c>usb_storage_locked</c> / <c>mobile_port_locked</c>), so the service applies the
/// correct lock state on its next start. Cloud registration is left pending — the
/// <c>SupabaseSyncWorker</c> picks it up within 30 seconds whenever the PC is online.
/// </summary>
public sealed class DeviceProvisioner
{
    public const string DefaultDeviceUnlockPin = "421301";
    public const string DefaultUninstallPin = "121356";

    private readonly ProgramDataPaths paths;
    private readonly DeviceControlDatabase database;
    private readonly IPinHasher pinHasher;
    private readonly ILogger<DeviceProvisioner> logger;

    public DeviceProvisioner(
        ProgramDataPaths paths,
        DeviceControlDatabase database,
        IPinHasher pinHasher,
        ILogger<DeviceProvisioner> logger)
    {
        this.paths = paths;
        this.database = database;
        this.pinHasher = pinHasher;
        this.logger = logger;
    }

    public async Task<ProvisionResult> ProvisionAsync(
        ProvisionRequest request,
        CancellationToken cancellationToken)
    {
        if (!PinPolicy.IsValid(request.DeviceUnlockPin) || !PinPolicy.IsValid(request.UninstallPin))
        {
            throw new ProvisionValidationException("Both PINs must be exactly 6 digits.");
        }

        if (request.DeviceUnlockPin == request.UninstallPin)
        {
            throw new ProvisionValidationException("Device unlock PIN and uninstall PIN must be different.");
        }

        var (recovered, recoveryDirectory) = await EnsureDatabaseUsableAsync(cancellationToken);

        var hadCredentials = await database.HasPinCredentialsAsync(cancellationToken);

        var email = (request.UserEmail ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            email = await database.GetPolicySettingAsync("user_email", "", cancellationToken);
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ProvisionValidationException(
                    "A valid Email ID is required for provisioning a fresh PC. Pass --email <id>.");
            }
        }

        if (!email.Contains('@') || !email.Contains('.'))
        {
            throw new ProvisionValidationException($"'{email}' is not a valid Email ID for registration.");
        }

        await database.SetPinCredentialAsync(
            PinPurpose.DeviceUnlock,
            pinHasher.Hash(request.DeviceUnlockPin),
            cancellationToken);
        await database.SetPinCredentialAsync(
            PinPurpose.Uninstall,
            pinHasher.Hash(request.UninstallPin),
            cancellationToken);
        await database.SetPolicySettingAsync("user_email", email, cancellationToken);
        await database.SetPolicySettingAsync("cloud_registration_pending", "true", cancellationToken);
        await database.SetPolicySettingAsync(
            "snapshot_monitoring_acknowledged",
            request.AcknowledgeSnapshots ? "true" : "false",
            cancellationToken);
        await database.SetPolicySettingAsync(
            "snapshot_enabled",
            request.AcknowledgeSnapshots ? "true" : "false",
            cancellationToken);

        var existingInterval = await database.GetPolicySettingAsync(
            "snapshot_interval_minutes", "", cancellationToken);
        if (!int.TryParse(existingInterval, out var parsedInterval) ||
            parsedInterval is < 1 or > 120)
        {
            await database.SetPolicySettingAsync("snapshot_interval_minutes", "5", cancellationToken);
        }

        await database.AppendActivityLogAsync(
            hadCredentials ? ActivityLogEventType.PolicyEvaluated : ActivityLogEventType.PinsInitialized,
            hadCredentials
                ? $"Device PINs were reset by zero-touch provisioning for Email '{email}' on PC '{Environment.MachineName}'."
                : $"Device protection provisioned for Email '{email}' on PC '{Environment.MachineName}'.",
            cancellationToken);

        var outcome = recovered
            ? ProvisionOutcome.RecoveredCorruptDatabase
            : hadCredentials ? ProvisionOutcome.UpdatedExisting : ProvisionOutcome.ProvisionedFresh;

        logger.LogInformation(
            "Provisioning complete for '{Email}' with outcome {Outcome}.", email, outcome);

        return new ProvisionResult(outcome, email, recovered, recoveryDirectory);
    }

    /// <summary>
    /// Makes sure the local SQLite database can be opened. Previous port-lock-only
    /// installs, interrupted writes, or permission damage can leave an unreadable file
    /// behind (including the classic SQLite Error 14 family). In that case the damaged
    /// files are quarantined under a timestamped recovery folder — never deleted —
    /// and a fresh database is created so provisioning always ends in a working state.
    /// </summary>
    private async Task<(bool Recovered, string? RecoveryDirectory)> EnsureDatabaseUsableAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await database.InitializeAsync(cancellationToken);
            await database.HasPinCredentialsAsync(cancellationToken);
            return (false, null);
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(
                ex,
                "Local database is unreadable and will be quarantined so provisioning can continue.");
        }

        var recoveryDirectory = Path.Combine(
            paths.BaseDirectory,
            "recovery",
            DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(recoveryDirectory);

        // The failed open above may have left a pooled shared-cache handle on the
        // file. Release it before moving anything.
        SqliteConnection.ClearAllPools();

        foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
        {
            var source = paths.DatabasePath + suffix;
            if (!File.Exists(source))
            {
                continue;
            }

            var destination = Path.Combine(
                recoveryDirectory, "secure-device-control.db" + suffix);
            File.Move(source, destination, overwrite: true);
        }

        await database.InitializeAsync(cancellationToken);
        return (true, recoveryDirectory);
    }
}
