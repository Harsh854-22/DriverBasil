using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using SecureDeviceControl.Shared.Ipc;

namespace SecureDeviceControl.Service.Ipc;

public sealed class NamedPipeServer
{
    private const int MaxFrameBytes = 64 * 1024;

    private readonly IpcRequestHandler requestHandler;
    private readonly ILogger<NamedPipeServer> logger;

    public NamedPipeServer(IpcRequestHandler requestHandler, ILogger<NamedPipeServer> logger)
    {
        this.requestHandler = requestHandler;
        this.logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var pipe = CreatePipe();
            await pipe.WaitForConnectionAsync(cancellationToken);
            await HandleClientAsync(pipe, cancellationToken);
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        try
        {
            var requestBytes = await ReadFrameAsync(pipe, cancellationToken);
            var request = JsonSerializer.Deserialize<IpcRequest>(requestBytes, IpcJson.Options)
                ?? throw new JsonException("Empty IPC request.");
            var response = await requestHandler.HandleAsync(request, cancellationToken);
            var responseBytes = JsonSerializer.SerializeToUtf8Bytes(response, IpcJson.Options);
            await pipe.WriteAsync(responseBytes, cancellationToken);
            await pipe.WriteAsync("\n"u8.ToArray(), cancellationToken);
            await pipe.FlushAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "IPC client request was rejected.");
        }
    }

    private static NamedPipeServerStream CreatePipe()
    {
        var pipeSecurity = new PipeSecurity();
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            IpcPipeNames.PipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 4,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 4096,
            outBufferSize: 4096,
            pipeSecurity);
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
                throw new InvalidOperationException("IPC frame exceeded the maximum allowed size.");
            }

            if (newlineIndex >= 0)
            {
                break;
            }
        }

        return memory.ToArray();
    }
}
