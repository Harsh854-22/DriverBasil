using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using SecureDeviceControl.Shared.Contracts;
using SecureDeviceControl.Shared.Ipc;

namespace SecureDeviceControl.Desktop;

public sealed class SnapshotCaptureService : IAsyncDisposable
{
    private readonly IpcClient ipcClient;
    private readonly CancellationTokenSource stoppingCts = new();
    private readonly object settingsLock = new();
    private Task? runTask;
    private bool enabled;
    private bool acknowledged;
    private int intervalMinutes = 5;
    private DateTimeOffset? lastSuccessfulCaptureAt;

    public SnapshotCaptureService(IpcClient ipcClient)
    {
        this.ipcClient = ipcClient;
    }

    public void UpdateSettings(bool snapshotEnabled, int snapshotIntervalMinutes, bool monitoringAcknowledged)
    {
        lock (settingsLock)
        {
            enabled = snapshotEnabled;
            acknowledged = monitoringAcknowledged;
            intervalMinutes = Math.Clamp(snapshotIntervalMinutes, 1, 120);
            runTask ??= Task.Run(RunAsync);
        }
    }

    private async Task RunAsync()
    {
        while (!stoppingCts.IsCancellationRequested)
        {
            try
            {
                var shouldCapture = false;
                lock (settingsLock)
                {
                    shouldCapture = enabled && acknowledged &&
                        (lastSuccessfulCaptureAt is null || DateTimeOffset.UtcNow - lastSuccessfulCaptureAt >= TimeSpan.FromMinutes(intervalMinutes));
                }

                if (shouldCapture)
                {
                    var imageBytes = CaptureVirtualScreenAsJpeg();
                    var response = await ipcClient.SendAsync(
                        IpcRequest.Create(
                            IpcOperation.UploadSnapshot,
                            new UploadSnapshotRequest(imageBytes, DateTimeOffset.UtcNow)),
                        stoppingCts.Token);

                    if (response.Success)
                    {
                        lock (settingsLock)
                        {
                            lastSuccessfulCaptureAt = DateTimeOffset.UtcNow;
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingCts.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // The service records failed uploads. The desktop app retries after a short delay.
            }

            try
            {
                TimeSpan delay;
                lock (settingsLock)
                {
                    delay = lastSuccessfulCaptureAt is null
                        ? TimeSpan.FromSeconds(5)
                        : TimeSpan.FromSeconds(30);
                }

                await Task.Delay(delay, stoppingCts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static byte[] CaptureVirtualScreenAsJpeg()
    {
        const int maximumDimension = 1920;
        var virtualScreen = System.Windows.Forms.SystemInformation.VirtualScreen;
        if (virtualScreen.Width <= 0 || virtualScreen.Height <= 0)
        {
            throw new InvalidOperationException("No active screen is available to capture.");
        }

        using var fullSizeBitmap = new Bitmap(virtualScreen.Width, virtualScreen.Height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(fullSizeBitmap))
        {
            graphics.CopyFromScreen(virtualScreen.Left, virtualScreen.Top, 0, 0, fullSizeBitmap.Size, CopyPixelOperation.SourceCopy);
        }

        var scale = Math.Min(1d, maximumDimension / (double)Math.Max(virtualScreen.Width, virtualScreen.Height));
        var targetWidth = Math.Max(1, (int)Math.Round(virtualScreen.Width * scale));
        var targetHeight = Math.Max(1, (int)Math.Round(virtualScreen.Height * scale));
        using var bitmap = new Bitmap(targetWidth, targetHeight, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.DrawImage(fullSizeBitmap, new Rectangle(0, 0, targetWidth, targetHeight));
        }

        using var output = new MemoryStream();
        var jpegCodec = ImageCodecInfo.GetImageEncoders().Single(codec => codec.FormatID == ImageFormat.Jpeg.Guid);
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(Encoder.Quality, 70L);
        bitmap.Save(output, jpegCodec, parameters);
        return output.ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        stoppingCts.Cancel();
        if (runTask is not null)
        {
            await runTask;
        }

        stoppingCts.Dispose();
    }
}
