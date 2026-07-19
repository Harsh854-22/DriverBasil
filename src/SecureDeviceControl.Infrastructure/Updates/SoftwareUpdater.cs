using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using SecureDeviceControl.Infrastructure.Paths;

namespace SecureDeviceControl.Infrastructure.Updates;

public sealed class SoftwareUpdater : ISoftwareUpdater
{
    private readonly ProgramDataPaths paths;
    private readonly ILogger<SoftwareUpdater> logger;
    private readonly HttpClient httpClient;

    public SoftwareUpdater(
        ProgramDataPaths paths,
        ILogger<SoftwareUpdater> logger,
        HttpClient? customHttpClient = null)
    {
        this.paths = paths;
        this.logger = logger;
        this.httpClient = customHttpClient ?? new HttpClient();
    }

    public async Task<bool> ApplyUpdateAsync(
        SoftwareUpdateModel updateModel,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(updateModel.DownloadUrl))
        {
            logger.LogWarning("Update download URL is empty.");
            return false;
        }

        var updatesDir = Path.Combine(paths.BaseDirectory, "updates");
        Directory.CreateDirectory(updatesDir);

        var tempFilePath = Path.Combine(updatesDir, $"update_{updateModel.Version}_{Guid.NewGuid():N}.exe");

        try
        {
            logger.LogInformation("Downloading software update v{Version} from '{Url}'...", updateModel.Version, updateModel.DownloadUrl);

            using (var response = await httpClient.GetAsync(updateModel.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await contentStream.CopyToAsync(fileStream, cancellationToken);
            }

            logger.LogInformation("Verifying SHA-256 checksum for downloaded update v{Version}...", updateModel.Version);

            if (!VerifySha256(tempFilePath, updateModel.Sha256Hash))
            {
                logger.LogError("SHA-256 hash mismatch for update v{Version}. Downloaded file may be corrupted or tampered with. Aborting upgrade.", updateModel.Version);
                try { File.Delete(tempFilePath); } catch { }
                return false;
            }

            logger.LogInformation("SHA-256 checksum verified successfully for v{Version}. Launching silent installer...", updateModel.Version);

            if (OperatingSystem.IsWindows())
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = tempFilePath,
                    Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process.Start(startInfo);
                logger.LogInformation("Launched update installer process for v{Version}.", updateModel.Version);
                return true;
            }
            else
            {
                logger.LogWarning("Silent software installation process launch is only supported on Windows OS.");
                return false;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download or apply software update v{Version}.", updateModel.Version);
            try { if (File.Exists(tempFilePath)) File.Delete(tempFilePath); } catch { }
            return false;
        }
    }

    public bool VerifySha256(string filePath, string expectedHash)
    {
        if (string.IsNullOrWhiteSpace(expectedHash) || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            using var sha = SHA256.Create();
            var hashBytes = sha.ComputeHash(stream);
            var computedHash = Convert.ToHexString(hashBytes);

            return string.Equals(computedHash, expectedHash.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error computing SHA-256 hash for file '{Path}'.", filePath);
            return false;
        }
    }
}
