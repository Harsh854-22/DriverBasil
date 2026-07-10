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
        await using var pipe = new NamedPipeClientStream(
            ".",
            IpcPipeNames.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        await pipe.ConnectAsync(2_000, cancellationToken);

        var requestBytes = JsonSerializer.SerializeToUtf8Bytes(request, IpcJson.Options);
        await pipe.WriteAsync(requestBytes, cancellationToken);
        await pipe.WriteAsync("\n"u8.ToArray(), cancellationToken);
        await pipe.FlushAsync(cancellationToken);

        var responseBytes = await ReadFrameAsync(pipe, cancellationToken);
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
