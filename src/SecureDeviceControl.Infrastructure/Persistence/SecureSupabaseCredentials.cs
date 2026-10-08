using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace SecureDeviceControl.Infrastructure.Persistence;

/// <summary>
/// Secure source of Supabase credentials for PCs in the field.
///
/// SECURITY MODEL: the release ZIP is handed to every PC, so anything baked into
/// <c>appsettings.json</c> must be treated as public. In particular a
/// <c>service_role</c> key or database password in shipped JSON lets anyone with
/// the release software list/download the private <c>Snapshots</c> bucket and
/// open the database directly. Therefore:
///
/// <list type="bullet">
/// <item>Secrets are NEVER read from <c>appsettings.json</c>. Only environment
/// variables (set at install time) and the restricted credentials file are used.</item>
/// <item>Snapshot uploads use the <c>anon</c> key (public by design) together with
/// an INSERT-only Storage policy, so a key extracted from the release can upload
/// JPEGs but can never list, download, or delete anyone's snapshots.</item>
/// <item>The <c>service_role</c> key lives ONLY on the admin's dashboard PC
/// (DPAPI-protected) and — as a legacy override — in a server environment
/// variable. It is never shipped.</item>
/// </list>
///
/// Without credentials the service runs in offline mode: USB/MTP port blocking,
/// PINs, and local logs keep working; cloud sync simply retries. Ports are never
/// affected by credential state.
/// </summary>
internal static class SecureSupabaseCredentials
{
    private const string DbEnvName = "ConnectionStrings__Supabase";
    private const string AnonKeyEnvName = "Supabase__StorageAnonKey";
    private const string ServiceRoleEnvName = "Supabase__StorageServiceRoleKey";

    private const string CredentialsFileName = "cloud-credentials.json";

    /// <summary>
    /// Database connection string from install-time sources only.
    /// Returns null when not configured (offline mode — ports stay blocked).
    /// </summary>
    public static string? GetDatabaseConnectionString()
    {
        var fromEnv = Environment.GetEnvironmentVariable(DbEnvName);
        if (IsUsableSecret(fromEnv))
        {
            return fromEnv;
        }

        var fromFile = ReadCredentialsFile()?.ConnectionString;
        return IsUsableSecret(fromFile) ? fromFile : null;
    }

    /// <summary>
    /// Upload key for the Snapshots bucket. Prefers the ship-safe anon key
    /// (baked placeholder or env), then the restricted file, then — only as a
    /// legacy admin override — the service_role key from the environment.
    /// The service_role key is NEVER taken from shipped JSON.
    /// Returns null when not configured (uploads disabled — ports stay blocked).
    /// </summary>
    public static string? GetStorageUploadKey(IConfiguration configuration)
    {
        var anonEnv = Environment.GetEnvironmentVariable(AnonKeyEnvName);
        if (IsUsableSecret(anonEnv))
        {
            return anonEnv;
        }

        var anonBaked = configuration["Supabase:StorageAnonKey"];
        if (IsUsableSecret(anonBaked))
        {
            return anonBaked;
        }

        var fromFile = ReadCredentialsFile()?.StorageKey;
        if (IsUsableSecret(fromFile))
        {
            return fromFile;
        }

        var roleEnv = Environment.GetEnvironmentVariable(ServiceRoleEnvName);
        return IsUsableSecret(roleEnv) ? roleEnv : null;
    }

    public static bool IsUsableSecret([NotNullWhen(true)] string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && !value.Contains('[')
            && !value.Contains("YOUR-PASSWORD", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("PASTE-", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Install-time credentials file written by an administrator:
    /// <c>%ProgramData%\SecureDeviceControl\cloud-credentials.json</c>
    /// (<c>{"ConnectionString":"...","StorageKey":"..."}</c>, both optional).
    /// The directory is ACL'd to SYSTEM+Administrators by the installer, so
    /// standard users cannot read it. A local administrator can still read
    /// anything on the PC — no software can prevent that; treat local admins
    /// as trusted and rotate keys if an admin machine is compromised.
    /// </summary>
    private static CredentialsFile? ReadCredentialsFile()
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "SecureDeviceControl",
                CredentialsFileName);
            if (!File.Exists(path))
            {
                return null;
            }

            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<CredentialsFile>(stream);
        }
        catch
        {
            return null;
        }
    }

    private sealed class CredentialsFile
    {
        public string? ConnectionString { get; set; }

        public string? StorageKey { get; set; }
    }
}
