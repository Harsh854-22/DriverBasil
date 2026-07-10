using System.Text.Json;
using Microsoft.Data.Sqlite;
using SecureDeviceControl.Domain.Activity;
using SecureDeviceControl.Infrastructure.Paths;
using SecureDeviceControl.Infrastructure.Security;
using SecureDeviceControl.Shared.Contracts;
using SecureDeviceControl.Shared.Ipc;
using SecureDeviceControl.Shared.Security;

namespace SecureDeviceControl.Infrastructure.Persistence;

public sealed class DeviceControlDatabase
{
    private readonly ProgramDataPaths paths;
    private readonly ISecretProtector secretProtector;

    public DeviceControlDatabase(ProgramDataPaths paths, ISecretProtector secretProtector)
    {
        this.paths = paths;
        this.secretProtector = secretProtector;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        paths.EnsureBaseDirectory();

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS PinCredentials (
                Purpose TEXT PRIMARY KEY NOT NULL,
                ProtectedPayload BLOB NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ActivityLogs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TimestampUtc TEXT NOT NULL,
                EventType TEXT NOT NULL,
                Message TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> HasPinCredentialsAsync(CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM PinCredentials;";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, null) > 0;
    }

    public async Task SetPinCredentialAsync(
        PinPurpose purpose,
        StoredPinCredential credential,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(credential, IpcJson.Options);
        var protectedPayload = secretProtector.Protect(payload);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO PinCredentials (Purpose, ProtectedPayload, UpdatedAtUtc)
            VALUES ($purpose, $protectedPayload, $updatedAtUtc)
            ON CONFLICT(Purpose) DO UPDATE SET
                ProtectedPayload = excluded.ProtectedPayload,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """;
        command.Parameters.AddWithValue("$purpose", purpose.ToString());
        command.Parameters.Add("$protectedPayload", SqliteType.Blob).Value = protectedPayload;
        command.Parameters.AddWithValue("$updatedAtUtc", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<StoredPinCredential?> GetPinCredentialAsync(
        PinPurpose purpose,
        CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ProtectedPayload
            FROM PinCredentials
            WHERE Purpose = $purpose
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$purpose", purpose.ToString());

        var protectedPayload = await command.ExecuteScalarAsync(cancellationToken);
        if (protectedPayload is not byte[] protectedBytes)
        {
            return null;
        }

        var payload = secretProtector.Unprotect(protectedBytes);
        return JsonSerializer.Deserialize<StoredPinCredential>(payload, IpcJson.Options);
    }

    public async Task AppendActivityLogAsync(
        ActivityLogEventType eventType,
        string message,
        CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ActivityLogs (TimestampUtc, EventType, Message)
            VALUES ($timestampUtc, $eventType, $message);
            """;
        command.Parameters.AddWithValue("$timestampUtc", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$eventType", eventType.ToString());
        command.Parameters.AddWithValue("$message", BoundMessage(message));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ActivityLogDto>> ListActivityLogsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var boundedLimit = Math.Clamp(limit, 1, 500);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, TimestampUtc, EventType, Message
            FROM ActivityLogs
            ORDER BY Id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", boundedLimit);

        var logs = new List<ActivityLogDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            logs.Add(new ActivityLogDto(
                reader.GetInt64(0),
                DateTimeOffset.Parse(reader.GetString(1), null),
                reader.GetString(2),
                reader.GetString(3)));
        }

        return logs;
    }

    private SqliteConnection CreateConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = paths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };

        return new SqliteConnection(builder.ToString());
    }

    private static string BoundMessage(string message)
    {
        return message.Length <= 1_000 ? message : message[..1_000];
    }
}
