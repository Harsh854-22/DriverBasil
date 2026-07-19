using Npgsql;
using Xunit;

namespace SecureDeviceControl.Service.Tests;

public sealed class SupabaseLiveEndToEndTests
{
    private const string ConnectionString = "Host=aws-1-ap-south-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.oyfczmpvmtwfynvloqkg;Password=LessgobasilDriver@1;SslMode=Require;";
    private readonly string testEmail = $"test_{Guid.NewGuid():N}@company.com";
    private readonly string testMachine = $"TEST-PC-{Guid.NewGuid():N}".Substring(0, 15);

    [Fact]
    public async Task LiveSupabase_All4Tables_CanInsertAndQuerySuccessfully()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        Assert.Equal(System.Data.ConnectionState.Open, connection.State);

        // Ensure activity_logs schema migration for machine_name and user_email columns
        try
        {
            await using var alterCmd1 = connection.CreateCommand();
            alterCmd1.CommandText = "ALTER TABLE activity_logs ADD COLUMN IF NOT EXISTS machine_name TEXT NOT NULL DEFAULT '';";
            await alterCmd1.ExecuteNonQueryAsync();

            await using var alterCmd2 = connection.CreateCommand();
            alterCmd2.CommandText = "ALTER TABLE activity_logs ADD COLUMN IF NOT EXISTS user_email TEXT NOT NULL DEFAULT '';";
            await alterCmd2.ExecuteNonQueryAsync();
        }
        catch
        {
            // Ignore if already added
        }

        var now = DateTimeOffset.UtcNow;

        // 1. Test registered_devices table
        await using var regCmd = connection.CreateCommand();
        regCmd.CommandText = """
            INSERT INTO registered_devices (email_id, machine_name, registered_at, updated_at)
            VALUES (@email_id, @machine_name, @now, @now);
            """;
        regCmd.Parameters.AddWithValue("@email_id", testEmail);
        regCmd.Parameters.AddWithValue("@machine_name", testMachine);
        regCmd.Parameters.AddWithValue("@now", now);
        var regRows = await regCmd.ExecuteNonQueryAsync();
        Assert.Equal(1, regRows);

        // 2. Test device_policies table (Website & Email restrictions)
        await using var polCmd = connection.CreateCommand();
        polCmd.CommandText = """
            INSERT INTO device_policies (email_id, machine_name, web_filter_mode, allowed_websites, blocked_websites, email_filter_mode, allowed_email_domains, updated_at)
            VALUES (@email_id, @machine_name, 'SELECTIVE', 'google.com, mail.google.com', 'facebook.com', 'RESTRICTED', 'gmail.com', @now);
            """;
        polCmd.Parameters.AddWithValue("@email_id", testEmail);
        polCmd.Parameters.AddWithValue("@machine_name", testMachine);
        polCmd.Parameters.AddWithValue("@now", now);
        var polRows = await polCmd.ExecuteNonQueryAsync();
        Assert.Equal(1, polRows);

        // Query policy back
        await using var readPolCmd = connection.CreateCommand();
        readPolCmd.CommandText = "SELECT web_filter_mode, allowed_websites, email_filter_mode, allowed_email_domains FROM device_policies WHERE email_id = @email_id;";
        readPolCmd.Parameters.AddWithValue("@email_id", testEmail);
        await using (var reader = await readPolCmd.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync());
            Assert.Equal("SELECTIVE", reader.GetString(0));
            Assert.Equal("google.com, mail.google.com", reader.GetString(1));
            Assert.Equal("RESTRICTED", reader.GetString(2));
            Assert.Equal("gmail.com", reader.GetString(3));
        }

        // 3. Test windows_password_commands table
        await using var passCmd = connection.CreateCommand();
        passCmd.CommandText = """
            INSERT INTO windows_password_commands (email_id, machine_name, target_username, new_password, status, created_at)
            VALUES (@email_id, @machine_name, 'TestUser', 'TestPass#2026!', 'PENDING', @now)
            RETURNING id;
            """;
        passCmd.Parameters.AddWithValue("@email_id", testEmail);
        passCmd.Parameters.AddWithValue("@machine_name", testMachine);
        passCmd.Parameters.AddWithValue("@now", now);
        var commandId = (long)(await passCmd.ExecuteScalarAsync() ?? 0L);
        Assert.True(commandId > 0);

        // 4. Test activity_logs table
        await using var logCmd = connection.CreateCommand();
        logCmd.CommandText = """
            INSERT INTO activity_logs (machine_name, user_email, timestamp_utc, event_type, message)
            VALUES (@machine_name, @user_email, @now, 'LiveTestVerification', 'End-to-End Supabase Live Integration Verification Succeeded.');
            """;
        logCmd.Parameters.AddWithValue("@machine_name", testMachine);
        logCmd.Parameters.AddWithValue("@user_email", testEmail);
        logCmd.Parameters.AddWithValue("@now", now);
        var logRows = await logCmd.ExecuteNonQueryAsync();
        Assert.Equal(1, logRows);

        // Cleanup test entries
        await using var cleanCmd = connection.CreateCommand();
        cleanCmd.CommandText = """
            DELETE FROM registered_devices WHERE email_id = @email_id;
            DELETE FROM device_policies WHERE email_id = @email_id;
            DELETE FROM windows_password_commands WHERE id = @cmd_id;
            """;
        cleanCmd.Parameters.AddWithValue("@email_id", testEmail);
        cleanCmd.Parameters.AddWithValue("@cmd_id", commandId);
        await cleanCmd.ExecuteNonQueryAsync();
    }
}
