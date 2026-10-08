using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SecureDeviceControl.SnapshotPortal.Storage;

public sealed class LocalDashboardSecrets
{
    public string SupabaseUrl { get; init; } = "https://oyfczmpvmtwfynvloqkg.supabase.co";
    public string? ServiceRoleKey { get; init; }

    public static LocalDashboardSecrets Load()
    {
        var url = Environment.GetEnvironmentVariable("SUPABASE_URL");
        var key = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY");

        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SecureDeviceControlDashboard",
            "settings.json");

        if (File.Exists(path))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                if (string.IsNullOrWhiteSpace(url) &&
                    root.TryGetProperty("SupabaseUrl", out var urlProperty))
                {
                    url = urlProperty.GetString();
                }

                if (string.IsNullOrWhiteSpace(key) &&
                    root.TryGetProperty("SupabaseServiceKeyEncrypted", out var encryptedProperty))
                {
                    key = Unprotect(encryptedProperty.GetString());
                }
            }
            catch
            {
                // Fall through to environment-only credentials.
            }
        }

        return new LocalDashboardSecrets
        {
            SupabaseUrl = string.IsNullOrWhiteSpace(url) ? "https://oyfczmpvmtwfynvloqkg.supabase.co" : url.Trim(),
            ServiceRoleKey = string.IsNullOrWhiteSpace(key) ? null : key.Trim()
        };
    }

    private static string? Unprotect(string? encrypted)
    {
        if (string.IsNullOrWhiteSpace(encrypted))
        {
            return null;
        }

        try
        {
            var protectedBytes = Convert.FromBase64String(encrypted);
            var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }
    }
}
