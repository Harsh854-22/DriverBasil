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
        // Overall operation timeout: 45 seconds max for any IPC round-trip.
        // The 75MB self-contained single-file EXE can take 15-30s to self-extract
        // on first launch, so we need generous time for initial service startup.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(45));
        var token = timeoutCts.Token;

        // Phase 1: Quick connect attempt (service already running)
        var pipe = CreatePipe();
        try
        {
            await pipe.ConnectAsync(1_500, token);
            return await SendOnPipeAsync(pipe, request, token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            await pipe.DisposeAsync();
        }

        // Phase 2: Service not running — auto-install and start
        ServiceInstallerHelper.EnsureServiceRunning();

        // Phase 3: Retry loop with backoff — wait for service to finish starting
        // The single-file EXE needs to self-extract .NET runtime on first run.
        const int maxRetries = 12;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            token.ThrowIfCancellationRequested();

            // Wait before retrying (2s between attempts)
            await Task.Delay(TimeSpan.FromSeconds(2), token);

            pipe = CreatePipe();
            try
            {
                await pipe.ConnectAsync(2_000, token);
                return await SendOnPipeAsync(pipe, request, token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch when (attempt < maxRetries)
            {
                await pipe.DisposeAsync();
                // Continue retrying
            }
            catch
            {
                await pipe.DisposeAsync();
                throw new TimeoutException(
                    "The background service did not start in time. " +
                    "Please right-click 'Install-Service.cmd' → Run as administrator, then try again.");
            }
        }

        throw new TimeoutException("Could not connect to the background service.");
    }

    private static NamedPipeClientStream CreatePipe()
    {
        return new NamedPipeClientStream(
            ".",
            IpcPipeNames.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
    }

    private static async Task<IpcResponse> SendOnPipeAsync(
        NamedPipeClientStream pipe,
        IpcRequest request,
        CancellationToken token)
    {
        await using (pipe)
        {
            var requestBytes = JsonSerializer.SerializeToUtf8Bytes(request, IpcJson.Options);
            await pipe.WriteAsync(requestBytes, token);
            await pipe.WriteAsync("\n"u8.ToArray(), token);
            await pipe.FlushAsync(token);

            var responseBytes = await ReadFrameAsync(pipe, token);
            return JsonSerializer.Deserialize<IpcResponse>(responseBytes, IpcJson.Options)
                ?? throw new InvalidOperationException("The service returned an empty response.");
        }
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
