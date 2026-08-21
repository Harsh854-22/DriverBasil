using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SecureDeviceControl.Infrastructure.Persistence;

public sealed class SupabaseSnapshotStorage : ISnapshotStorage
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private readonly IConfiguration configuration;
    private readonly ILogger<SupabaseSnapshotStorage> logger;

    public SupabaseSnapshotStorage(
        IConfiguration configuration,
        ILogger<SupabaseSnapshotStorage> logger)
    {
        this.configuration = configuration;
        this.logger = logger;
    }

    public async Task UploadAsync(string objectKey, byte[] imageBytes, CancellationToken cancellationToken)
    {
        var baseUrl = configuration["Supabase:Url"];
        var bucket = configuration["Supabase:StorageBucket"];
        var serviceRoleKey = configuration["Supabase:StorageServiceRoleKey"];

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var projectUri) || projectUri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(bucket) || string.IsNullOrWhiteSpace(serviceRoleKey))
        {
            throw new InvalidOperationException("Supabase Storage is not configured.");
        }

        var escapedKey = string.Join('/', objectKey.Split('/').Select(Uri.EscapeDataString));
        var uploadUri = new Uri(projectUri, $"storage/v1/object/{Uri.EscapeDataString(bucket)}/{escapedKey}");

        using var request = new HttpRequestMessage(HttpMethod.Post, uploadUri)
        {
            Content = new ByteArrayContent(imageBytes)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        request.Headers.Add("apikey", serviceRoleKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceRoleKey);
        request.Headers.Add("x-upsert", "false");

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        logger.LogWarning("Supabase Storage rejected a snapshot upload with HTTP status {StatusCode}.", (int)response.StatusCode);
        throw new InvalidOperationException("Snapshot upload was rejected by Supabase Storage.");
    }
}
