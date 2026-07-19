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
}
