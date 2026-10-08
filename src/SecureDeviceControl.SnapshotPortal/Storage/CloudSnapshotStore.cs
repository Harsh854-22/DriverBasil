using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SecureDeviceControl.SnapshotPortal.Storage;

public sealed class CloudSnapshotStore : IDisposable
{
    private const string Bucket = "Snapshots";
    private const int PageSize = 100;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient httpClient;
    private readonly bool configured;

    public CloudSnapshotStore(LocalDashboardSecrets secrets)
    {
        configured = Uri.TryCreate(secrets.SupabaseUrl, UriKind.Absolute, out var uri) &&
                     uri.Scheme == Uri.UriSchemeHttps &&
                     !string.IsNullOrWhiteSpace(secrets.ServiceRoleKey);

        httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        if (configured)
        {
            httpClient.BaseAddress = new Uri(secrets.SupabaseUrl);
            httpClient.DefaultRequestHeaders.Add("apikey", secrets.ServiceRoleKey);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secrets.ServiceRoleKey);
        }
    }

    public bool IsConfigured => configured;

    public async Task<IReadOnlyList<SnapshotFrame>> ListFramesAsync(CancellationToken cancellationToken)
    {
        if (!configured)
        {
            throw new InvalidOperationException("Cloud storage is not configured on this PC.");
        }

        var keys = new List<string>();
        await WalkAsync("", 0, keys, cancellationToken);

        return keys
            .Select(SnapshotCatalog.TryParse)
            .Where(frame => frame is not null)
            .Select(frame => frame!)
            .OrderByDescending(frame => frame.CapturedAtUtc)
            .Take(2000)
            .ToList();
    }

    public async Task<byte[]> DownloadAsync(string objectKey, CancellationToken cancellationToken)
    {
        if (SnapshotCatalog.TryParse(objectKey) is null)
        {
            throw new InvalidOperationException("Unknown snapshot.");
        }

        var escapedKey = string.Join('/', objectKey.Split('/').Select(Uri.EscapeDataString));
        using var response = await httpClient.GetAsync($"storage/v1/object/{Bucket}/{escapedKey}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("The cloud bucket did not return that snapshot.");
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.Length < 3 || bytes[0] != 0xFF || bytes[1] != 0xD8 || bytes[2] != 0xFF)
        {
            throw new InvalidOperationException("The stored object is not a JPEG.");
        }

        return bytes;
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        if (SnapshotCatalog.TryParse(objectKey) is null)
        {
            throw new InvalidOperationException("Unknown snapshot.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"storage/v1/object/{Bucket}")
        {
            Content = new StringContent(JsonSerializer.Serialize(new[] { objectKey }), Encoding.UTF8, "application/json")
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var escapedKey = string.Join('/', objectKey.Split('/').Select(Uri.EscapeDataString));
        using var fallback = new HttpRequestMessage(HttpMethod.Delete, $"storage/v1/object/{Bucket}/{escapedKey}");
        using var fallbackResponse = await httpClient.SendAsync(fallback, cancellationToken);
        if (!fallbackResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("The cloud bucket refused the delete.");
        }
    }

    private async Task WalkAsync(string prefix, int depth, List<string> keys, CancellationToken cancellationToken)
    {
        if (depth > 4 || keys.Count >= 2000)
        {
            return;
        }

        var offset = 0;
        while (keys.Count < 2000)
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

            var entries = await JsonSerializer.DeserializeAsync<List<BucketEntry>>(
                              await response.Content.ReadAsStreamAsync(cancellationToken),
                              JsonOptions,
                              cancellationToken)
                          ?? new List<BucketEntry>();

            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Name) || entry.Name is "." or "..")
                {
                    continue;
                }

                var child = prefix + entry.Name;
                if (entry.Id is null)
                {
                    await WalkAsync(child + "/", depth + 1, keys, cancellationToken);
                }
                else if (entry.Name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                {
                    keys.Add(child);
                }
            }

            if (entries.Count < PageSize)
            {
                break;
            }

            offset += PageSize;
        }
    }

    public void Dispose() => httpClient.Dispose();

    private sealed class BucketEntry
    {
        public string? Name { get; set; }
        public string? Id { get; set; }
    }
}
