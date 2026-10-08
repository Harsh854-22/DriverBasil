using System.Text.Json;
using SecureDeviceControl.Infrastructure.Paths;
using SecureDeviceControl.Infrastructure.Persistence;
using SecureDeviceControl.Infrastructure.Security;

namespace SecureDeviceControl.Service.Provisioning;

/// <summary>
/// Handles the <c>SecureDeviceControl.Service.exe provision ...</c> command line.
/// Exit codes: 0 = success, 1 = unexpected failure, 2 = validation/usage error,
/// 3 = success but the previous local database was corrupt and got quarantined.
/// </summary>
public static class ProvisionCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var dataDir = GetOption(args, "data-dir");
        if (args.Any(a => IsFlag(a, "status")))
        {
            return await PrintStatusAsync(dataDir);
        }

        var options = Parse(args);
        if (options is null)
        {
            await Console.Error.WriteLineAsync(Usage());
            return 2;
        }

        try
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton(
                string.IsNullOrWhiteSpace(dataDir)
                    ? new ProgramDataPaths()
                    : new ProgramDataPaths(dataDir));
            builder.Services.AddSingleton<ISecretProtector, DpapiSecretProtector>();
            builder.Services.AddSingleton<IPinHasher, Argon2idPinHasher>();
            builder.Services.AddSingleton<DeviceControlDatabase>();
            builder.Services.AddSingleton<DeviceProvisioner>();

            using var host = builder.Build();
            var provisioner = host.Services.GetRequiredService<DeviceProvisioner>();

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var result = await provisioner.ProvisionAsync(
                new ProvisionRequest(
                    options.Email,
                    options.DevicePin,
                    options.UninstallPin,
                    options.AcknowledgeSnapshots),
                cts.Token);

            Console.WriteLine($"Provisioning complete: {result.Outcome} for '{result.UserEmail}'.");
            if (result.DatabaseWasRecovered)
            {
                Console.WriteLine(
                    $"Previous database was unreadable and was preserved in: {result.RecoveryDirectory}");
                return 3;
            }

            return 0;
        }
        catch (ProvisionValidationException ex)
        {
            await Console.Error.WriteLineAsync($"Provisioning failed: {ex.Message}");
            return 2;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Provisioning failed unexpectedly: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> PrintStatusAsync(string? dataDir)
    {
        try
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Services.AddSingleton(
                string.IsNullOrWhiteSpace(dataDir)
                    ? new ProgramDataPaths()
                    : new ProgramDataPaths(dataDir));
            builder.Services.AddSingleton<ISecretProtector, DpapiSecretProtector>();
            builder.Services.AddSingleton<DeviceControlDatabase>();

            using var host = builder.Build();
            var database = host.Services.GetRequiredService<DeviceControlDatabase>();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await database.InitializeAsync(cts.Token);
            var initialized = await database.HasPinCredentialsAsync(cts.Token);
            var email = initialized
                ? await database.GetPolicySettingAsync("user_email", "", cts.Token)
                : "";

            Console.WriteLine(JsonSerializer.Serialize(new { initialized, email }));
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                initialized = false,
                email = "",
                error = ex.Message
            }));
            return 0;
        }
    }

    private static ProvisionOptions? Parse(string[] args)
    {
        string? email = null;
        string? devicePin = null;
        string? uninstallPin = null;
        bool acknowledgeSnapshots = false;

        for (var i = 0; i < args.Length; i++)
        {
            string? value = null;
            var key = args[i];
            var equalsIndex = key.IndexOf('=');
            if (equalsIndex > 0)
            {
                value = key[(equalsIndex + 1)..];
                key = key[..equalsIndex];
            }

            string? NextValue()
            {
                if (value is not null)
                {
                    return value;
                }

                if (i + 1 < args.Length)
                {
                    i++;
                    return args[i];
                }

                return null;
            }

            if (IsFlag(key, "email"))
            {
                email = NextValue();
            }
            else if (IsFlag(key, "data-dir"))
            {
                _ = NextValue();
            }
            else if (IsFlag(key, "device-pin"))
            {
                devicePin = NextValue();
            }
            else if (IsFlag(key, "uninstall-pin"))
            {
                uninstallPin = NextValue();
            }
            else if (IsFlag(key, "snapshot-ack"))
            {
                var raw = NextValue();
                if (!bool.TryParse(raw, out acknowledgeSnapshots) &&
                    !TryParseBit(raw, out acknowledgeSnapshots))
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        if (string.IsNullOrWhiteSpace(devicePin) || string.IsNullOrWhiteSpace(uninstallPin))
        {
            return null;
        }

        return new ProvisionOptions(email, devicePin.Trim(), uninstallPin.Trim(), acknowledgeSnapshots);
    }

    private static bool IsFlag(string arg, string name)
    {
        return arg.Equals("--" + name, StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i];
            var equalsIndex = key.IndexOf('=');
            if (equalsIndex > 0 && key[..equalsIndex].Equals("--" + name, StringComparison.OrdinalIgnoreCase))
            {
                return key[(equalsIndex + 1)..];
            }

            if (key.Equals("--" + name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static bool TryParseBit(string? raw, out bool result)
    {
        if (raw == "1")
        {
            result = true;
            return true;
        }

        if (raw == "0")
        {
            result = false;
            return true;
        }

        result = false;
        return false;
    }

    private static string Usage()
    {
        return """
            Usage:
              SecureDeviceControl.Service.exe provision --email <id> --device-pin <6 digits> --uninstall-pin <6 digits> [--snapshot-ack true|false]
              SecureDeviceControl.Service.exe provision --status

            An empty --email keeps the Email ID already stored on the PC (fails on a fresh PC).
            --data-dir <path> overrides the data directory (support/testing only).
            Exit codes: 0 ok · 3 ok, corrupt database was quarantined · 2 usage/validation · 1 unexpected.
            """;
    }

    private sealed record ProvisionOptions(
        string? Email,
        string DevicePin,
        string UninstallPin,
        bool AcknowledgeSnapshots);
}
