using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using SecureDeviceControl.Shared.Ipc;

namespace SecureDeviceControl.Desktop;

public sealed class IpcClient
{
    private const int MaxFrameBytes = 64 * 1024;

    public async Task<IpcResponse> SendAsync(IpcRequest request, CancellationToken cancellationToken = default)
    {
        // Overall operation timeout: 30 seconds max for any IPC round-trip
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
        var token = timeoutCts.Token;

        await using var pipe = new NamedPipeClientStream(
            ".",
            IpcPipeNames.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        try
        {
            await pipe.ConnectAsync(1_500, token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // Caller cancelled, propagate immediately
        }
        catch
        {
            // Auto-install and start service via UAC elevation helper if service isn't running
            ServiceInstallerHelper.EnsureServiceRunning();
            await pipe.ConnectAsync(4_000, token);
        }

        var requestBytes = JsonSerializer.SerializeToUtf8Bytes(request, IpcJson.Options);
        await pipe.WriteAsync(requestBytes, token);
        await pipe.WriteAsync("\n"u8.ToArray(), token);
        await pipe.FlushAsync(token);

        var responseBytes = await ReadFrameAsync(pipe, token);
        return JsonSerializer.Deserialize<IpcResponse>(responseBytes, IpcJson.Options)
            ?? throw new InvalidOperationException("The service returned an empty response.");
    }

    private static async Task<byte[]> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[1024];

        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            var newlineIndex = Array.IndexOf(buffer, (byte)'\n', 0, read);
            var count = newlineIndex >= 0 ? newlineIndex : read;
            memory.Write(buffer, 0, count);

            if (memory.Length > MaxFrameBytes)
            {
                throw new InvalidOperationException("The service response was too large.");
            }

            if (newlineIndex >= 0)
            {
                break;
            }
        }

        return memory.ToArray();
    }
}
