using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SecureDeviceControl.Dashboard.Settings;

public sealed class DashboardSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string SupabaseUrl { get; set; } = "https://oyfczmpvmtwfynvloqkg.supabase.co";
    public string SupabaseServiceKeyEncrypted { get; set; } = "";
    public string AdminUsername { get; set; } = "";
    public string AdminPasswordHash { get; set; } = "";
    public int SimilarityThreshold { get; set; } = 8;

    public static string SettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SecureDeviceControlDashboard");

    public static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public static string CacheDirectory => Path.Combine(SettingsDirectory, "snapshot-cache");

    public string? SupabaseServiceKey
    {
        get => Unprotect(SupabaseServiceKeyEncrypted);
        set => SupabaseServiceKeyEncrypted = Protect(value ?? "");
    }

    public bool HasAdminAccount =>
        !string.IsNullOrWhiteSpace(AdminUsername) && !string.IsNullOrWhiteSpace(AdminPasswordHash);

    public static DashboardSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var loaded = JsonSerializer.Deserialize<DashboardSettings>(File.ReadAllText(SettingsPath));
                if (loaded is not null)
                {
                    return loaded;
                }
            }
        }
        catch
        {
            // A corrupted settings file falls back to defaults so the app can still start.
        }

        return new DashboardSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
    }

    private static string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return "";
        }

        var bytes = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    private static string? Unprotect(string encrypted)
    {
        if (string.IsNullOrEmpty(encrypted))
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
