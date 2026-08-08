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
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(8));
        var token = timeoutCts.Token;

        await using var pipe = CreatePipe();
        try
        {
            await pipe.ConnectAsync(5_000, token);
            return await SendOnPipeAsync(pipe, request, token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(
                "The protection service did not respond. An administrator must install or repair the service before using this app.");
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException(
                "Windows denied access to the protection service. Ask an administrator to run the repair installer.",
                ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException(
                "The protection service is unavailable. Ask an administrator to run Install-Service.ps1.",
                ex);
        }
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
