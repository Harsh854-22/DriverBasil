using Npgsql;

namespace SecureDeviceControl.Service.Tests;

public sealed class SupabaseConnectionIntegrationTests
{
    private const string ConnectionString = "Host=aws-1-ap-south-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.oyfczmpvmtwfynvloqkg;Password=LessgobasilDriver@1;SslMode=Require;";

    [Fact]
    public async Task Can_Connect_To_Supabase_And_Ensure_ActivityLogs_Table()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        Assert.Equal(System.Data.ConnectionState.Open, connection.State);

        // Ensure table exists
        await using var createTableCmd = connection.CreateCommand();
        createTableCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS activity_logs (
                id BIGSERIAL PRIMARY KEY,
                timestamp_utc TIMESTAMPTZ NOT NULL,
                event_type TEXT NOT NULL,
                message TEXT NOT NULL
            );
            """;
        await createTableCmd.ExecuteNonQueryAsync();

        // Insert test log record
        await using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO activity_logs (timestamp_utc, event_type, message)
            VALUES (@timestamp_utc, @event_type, @message);
            """;
        insertCmd.Parameters.AddWithValue("@timestamp_utc", DateTimeOffset.UtcNow);
        insertCmd.Parameters.AddWithValue("@event_type", "TestVerification");
        insertCmd.Parameters.AddWithValue("@message", "Supabase database connection and log sync verified successfully.");
        var rowsInserted = await insertCmd.ExecuteNonQueryAsync();

        Assert.Equal(1, rowsInserted);

        // Query count from activity_logs
        await using var countCmd = connection.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM activity_logs;";
        var totalLogs = (long)(await countCmd.ExecuteScalarAsync() ?? 0L);

        Assert.True(totalLogs > 0, "Activity logs table should contain at least 1 record.");
    }
}
