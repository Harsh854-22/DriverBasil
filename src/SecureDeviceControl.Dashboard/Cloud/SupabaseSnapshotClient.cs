using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SecureDeviceControl.Dashboard.Cloud;

public sealed class SupabaseSnapshotClient : IDisposable
{
    private const string Bucket = "Snapshots";
    private const int PageSize = 1000;

    private readonly HttpClient httpClient;

    public SupabaseSnapshotClient(string supabaseUrl, string serviceRoleKey)
    {
        if (!Uri.TryCreate(supabaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Supabase URL must be a valid https address.", nameof(supabaseUrl));
        }

        if (string.IsNullOrWhiteSpace(serviceRoleKey))
        {
            throw new ArgumentException("A Supabase service role key is required.", nameof(serviceRoleKey));
        }

        httpClient = new HttpClient { BaseAddress = uri, Timeout = TimeSpan.FromSeconds(60) };
        httpClient.DefaultRequestHeaders.Add("apikey", serviceRoleKey);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", serviceRoleKey);
    }

    public async Task<IReadOnlyList<DeviceInfo>> GetRegisteredDevicesAsync(CancellationToken cancellationToken)
    {
        var devices = new List<DeviceInfo>();
        const int pageSize = 500;
        var offset = 0;

        while (true)
        {
            using var response = await httpClient.GetAsync(
                $"rest/v1/registered_devices?select=email_id,machine_name,updated_at&order=updated_at.desc&limit={pageSize}&offset={offset}",
                cancellationToken);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var rows = await JsonSerializer.DeserializeAsync<List<RegisteredDeviceRow>>(stream, cancellationToken: cancellationToken)
                       ?? new List<RegisteredDeviceRow>();

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.EmailId) || string.IsNullOrWhiteSpace(row.MachineName))
                {
                    continue;
                }

                var email = row.EmailId.Trim().ToLowerInvariant();
                devices.Add(new DeviceInfo(
                    email,
                    row.MachineName,
                    DeviceInfo.ComputeDeviceHash(row.MachineName, email),
                    row.UpdatedAt));
            }

            if (rows.Count < pageSize)
            {
                break;
            }

            offset += pageSize;
        }

        return devices;
    }

    public async Task<IReadOnlyList<string>> ListObjectKeysAsync(string prefix, CancellationToken cancellationToken)
    {
        var keys = new List<string>();
        var offset = 0;

        while (true)
        {
            var body = JsonSerializer.Serialize(new
            {
                prefix,
                limit = PageSize,
                offset,
                sortBy = new { column = "name", order = "asc" }
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, $"storage/v1/object/list/{Bucket}")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var entries = await JsonSerializer.DeserializeAsync<List<BucketListEntry>>(stream, cancellationToken: cancellationToken)
                          ?? new List<BucketListEntry>();

            foreach (var entry in entries)
            {
                if (!string.IsNullOrWhiteSpace(entry.Name))
                {
                    keys.Add(prefix + entry.Name);
                }
            }

            if (entries.Count < PageSize)
            {
                break;
            }

            offset += PageSize;
        }

        return keys;
    }

    public async Task<byte[]> DownloadSnapshotAsync(string objectKey, CancellationToken cancellationToken)
    {
        var escapedKey = string.Join('/', objectKey.Split('/').Select(Uri.EscapeDataString));
        using var response = await httpClient.GetAsync($"storage/v1/object/{Bucket}/{escapedKey}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }

    private sealed class RegisteredDeviceRow
    {
        public string EmailId { get; set; } = "";
        public string MachineName { get; set; } = "";
        public DateTimeOffset UpdatedAt { get; set; }
    }

    private sealed class BucketListEntry
    {
        public string Name { get; set; } = "";
    }
}
