using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using SecureDeviceControl.Domain.Activity;

namespace SecureDeviceControl.Infrastructure.Persistence;

public sealed class PostgresCloudRepository : ICloudRepository
{
    private readonly IConfiguration configuration;
    private readonly ILogger<PostgresCloudRepository> logger;
    private bool schemaEnsured = false;

    public PostgresCloudRepository(
        IConfiguration configuration,
        ILogger<PostgresCloudRepository> logger)
    {
        this.configuration = configuration;
        this.logger = logger;
    }

    private string? GetConnectionString()
    {
        return configuration.GetConnectionString("Supabase")
            ?? configuration.GetConnectionString("Postgres");
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (schemaEnsured) return;

        var connStr = GetConnectionString();
        if (string.IsNullOrWhiteSpace(connStr) || connStr.Contains("[YOUR-PASSWORD]"))
        {
            return;
        }

        await using var connection = new NpgsqlConnection(connStr);
        await connection.OpenAsync(cancellationToken);

        await using var createCmd = connection.CreateCommand();
        createCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS activity_logs (
                id BIGSERIAL PRIMARY KEY,
                machine_name TEXT NOT NULL DEFAULT '',
                user_email TEXT NOT NULL DEFAULT '',
                timestamp_utc TIMESTAMPTZ NOT NULL,
                event_type TEXT NOT NULL,
                message TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS registered_devices (
                email_id TEXT PRIMARY KEY,
                machine_name TEXT NOT NULL,
                registered_at TIMESTAMPTZ NOT NULL,
                updated_at TIMESTAMPTZ NOT NULL
            );

            CREATE TABLE IF NOT EXISTS device_policies (
                email_id TEXT PRIMARY KEY,
                machine_name TEXT NOT NULL,
                web_filter_mode TEXT NOT NULL DEFAULT 'OFF',
                allowed_websites TEXT NOT NULL DEFAULT '',
                blocked_websites TEXT NOT NULL DEFAULT '',
                email_filter_mode TEXT NOT NULL DEFAULT 'OFF',
                allowed_email_domains TEXT NOT NULL DEFAULT 'company.com',
                updated_at TIMESTAMPTZ NOT NULL
            );

            CREATE TABLE IF NOT EXISTS windows_password_commands (
                id BIGSERIAL PRIMARY KEY,
                email_id TEXT NOT NULL,
                machine_name TEXT NOT NULL,
                target_username TEXT NOT NULL,
                new_password TEXT NOT NULL,
                status TEXT NOT NULL DEFAULT 'PENDING',
                error_message TEXT,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                executed_at TIMESTAMPTZ
            );

            CREATE TABLE IF NOT EXISTS software_updates (
                id BIGSERIAL PRIMARY KEY,
                version TEXT NOT NULL,
                download_url TEXT NOT NULL,
                sha256_hash TEXT NOT NULL,
                mandatory BOOLEAN NOT NULL DEFAULT FALSE,
                target_machine TEXT NOT NULL DEFAULT 'ALL',
                released_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            """;
        await createCmd.ExecuteNonQueryAsync(cancellationToken);

        try
        {
            await using var alterCmd1 = connection.CreateCommand();
            alterCmd1.CommandText = "ALTER TABLE activity_logs ADD COLUMN IF NOT EXISTS machine_name TEXT NOT NULL DEFAULT '';";
            await alterCmd1.ExecuteNonQueryAsync(cancellationToken);

            await using var alterCmd2 = connection.CreateCommand();
            alterCmd2.CommandText = "ALTER TABLE activity_logs ADD COLUMN IF NOT EXISTS user_email TEXT NOT NULL DEFAULT '';";
            await alterCmd2.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
            // Best-effort schema migration
        }

        schemaEnsured = true;
    }

    public async Task RegisterDeviceAsync(
        string emailId,
        string machineName,
        CancellationToken cancellationToken)
    {
        var connStr = GetConnectionString();
        if (string.IsNullOrWhiteSpace(connStr) || connStr.Contains("[YOUR-PASSWORD]"))
        {
            return;
        }

        await EnsureSchemaAsync(cancellationToken);

        await using var connection = new NpgsqlConnection(connStr);
        await connection.OpenAsync(cancellationToken);

        var normalizedEmail = emailId.ToLowerInvariant().Trim();

        // Check single-PC binding rule
        await using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "SELECT machine_name FROM registered_devices WHERE email_id = @email_id LIMIT 1;";
        checkCmd.Parameters.AddWithValue("@email_id", normalizedEmail);

        var existingMachineObj = await checkCmd.ExecuteScalarAsync(cancellationToken);
        if (existingMachineObj is string existingMachine && !string.Equals(existingMachine, machineName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"This Email ID ({emailId}) is already registered to PC: '{existingMachine}'. An Email ID cannot be registered on multiple PCs.");
        }

        await using var upsertCmd = connection.CreateCommand();
        upsertCmd.CommandText = """
            INSERT INTO registered_devices (email_id, machine_name, registered_at, updated_at)
            VALUES (@email_id, @machine_name, @now, @now)
            ON CONFLICT (email_id) DO UPDATE SET
                machine_name = excluded.machine_name,
                updated_at = excluded.updated_at;

            INSERT INTO device_policies (email_id, machine_name, web_filter_mode, allowed_websites, blocked_websites, email_filter_mode, allowed_email_domains, updated_at)
            VALUES (@email_id, @machine_name, 'OFF', '', '', 'OFF', 'company.com', @now)
            ON CONFLICT (email_id) DO NOTHING;
            """;
        upsertCmd.Parameters.AddWithValue("@email_id", normalizedEmail);
        upsertCmd.Parameters.AddWithValue("@machine_name", machineName);
        upsertCmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow);
        await upsertCmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UploadActivityLogsAsync(
        IReadOnlyList<ActivityLogEntry> logs,
        CancellationToken cancellationToken)
    {
        if (logs.Count == 0) return;

        var connStr = GetConnectionString();
        if (string.IsNullOrWhiteSpace(connStr) || connStr.Contains("[YOUR-PASSWORD]"))
        {
            return;
        }

        await EnsureSchemaAsync(cancellationToken);

        await using var connection = new NpgsqlConnection(connStr);
        await connection.OpenAsync(cancellationToken);

        foreach (var log in logs)
        {
            await using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = """
                INSERT INTO activity_logs (machine_name, user_email, timestamp_utc, event_type, message)
                VALUES (@machine_name, @user_email, @timestamp_utc, @event_type, @message);
                """;
            insertCmd.Parameters.AddWithValue("@machine_name", log.MachineName);
            insertCmd.Parameters.AddWithValue("@user_email", log.UserEmail);
            insertCmd.Parameters.AddWithValue("@timestamp_utc", log.Timestamp);
            insertCmd.Parameters.AddWithValue("@event_type", log.EventType.ToString());
            insertCmd.Parameters.AddWithValue("@message", log.Message);

            await insertCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task<CloudDevicePolicy?> GetDevicePolicyAsync(
        string emailId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(emailId)) return null;

        var connStr = GetConnectionString();
        if (string.IsNullOrWhiteSpace(connStr) || connStr.Contains("[YOUR-PASSWORD]"))
        {
            return null;
        }

        await EnsureSchemaAsync(cancellationToken);

        await using var connection = new NpgsqlConnection(connStr);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT email_id, machine_name, web_filter_mode, allowed_websites, blocked_websites, email_filter_mode, allowed_email_domains
            FROM device_policies
            WHERE email_id = @email_id
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("@email_id", emailId.ToLowerInvariant().Trim());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new CloudDevicePolicy(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6));
        }

        return null;
    }

    public async Task<IReadOnlyList<WindowsPasswordCommand>> GetPendingWindowsPasswordCommandsAsync(
        string emailId,
        string machineName,
        CancellationToken cancellationToken)
    {
        var list = new List<WindowsPasswordCommand>();
        var connStr = GetConnectionString();
        if (string.IsNullOrWhiteSpace(connStr) || connStr.Contains("[YOUR-PASSWORD]"))
        {
            return list;
        }

        await EnsureSchemaAsync(cancellationToken);

        await using var connection = new NpgsqlConnection(connStr);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, email_id, machine_name, target_username, new_password, status, error_message
            FROM windows_password_commands
            WHERE email_id = @email_id AND machine_name = @machine_name AND status = 'PENDING'
            ORDER BY id ASC;
            """;
        cmd.Parameters.AddWithValue("@email_id", emailId.ToLowerInvariant().Trim());
        cmd.Parameters.AddWithValue("@machine_name", machineName);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new WindowsPasswordCommand(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        return list;
    }

    public async Task UpdateWindowsPasswordCommandStatusAsync(
        long commandId,
        string status,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        var connStr = GetConnectionString();
        if (string.IsNullOrWhiteSpace(connStr) || connStr.Contains("[YOUR-PASSWORD]"))
        {
            return;
        }

        await EnsureSchemaAsync(cancellationToken);

        await using var connection = new NpgsqlConnection(connStr);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE windows_password_commands
            SET status = @status,
                error_message = @error_message,
                executed_at = @now
            WHERE id = @id;
            """;
        cmd.Parameters.AddWithValue("@id", commandId);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@error_message", (object?)errorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<SecureDeviceControl.Infrastructure.Updates.SoftwareUpdateModel?> GetLatestSoftwareUpdateAsync(
        string machineName,
        CancellationToken cancellationToken)
    {
        var connStr = GetConnectionString();
        if (string.IsNullOrWhiteSpace(connStr) || connStr.Contains("[YOUR-PASSWORD]"))
        {
            return null;
        }

        await EnsureSchemaAsync(cancellationToken);

        await using var connection = new NpgsqlConnection(connStr);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, version, download_url, sha256_hash, mandatory, target_machine, released_at
            FROM software_updates
            WHERE target_machine = 'ALL' OR LOWER(target_machine) = LOWER(@machine_name)
            ORDER BY id DESC
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("@machine_name", machineName);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new SecureDeviceControl.Infrastructure.Updates.SoftwareUpdateModel(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetBoolean(4),
                reader.GetString(5),
                reader.GetDateTime(6));
        }

        return null;
    }
}
