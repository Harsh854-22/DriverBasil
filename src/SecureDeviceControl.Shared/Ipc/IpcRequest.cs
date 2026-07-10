using System.Text.Json;

namespace SecureDeviceControl.Shared.Ipc;

public sealed record IpcRequest(
    IpcOperation Operation,
    string CorrelationId,
    string? SessionToken = null,
    JsonElement? Payload = null)
{
    public static IpcRequest Create<TPayload>(
        IpcOperation operation,
        TPayload payload,
        string? sessionToken = null)
    {
        var element = JsonSerializer.SerializeToElement(payload, IpcJson.Options);
        return new IpcRequest(operation, Guid.NewGuid().ToString("N"), sessionToken, element);
    }

    public static IpcRequest Create(IpcOperation operation, string? sessionToken = null)
    {
        return new IpcRequest(operation, Guid.NewGuid().ToString("N"), sessionToken);
    }
}
