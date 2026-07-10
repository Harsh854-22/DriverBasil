using System.Text.Json;

namespace SecureDeviceControl.Shared.Ipc;

public sealed record IpcResponse(
    bool Success,
    string CorrelationId,
    IpcErrorCode ErrorCode = IpcErrorCode.None,
    string? Message = null,
    JsonElement? Payload = null)
{
    public static IpcResponse Ok<TPayload>(string correlationId, TPayload payload)
    {
        var element = JsonSerializer.SerializeToElement(payload, IpcJson.Options);
        return new IpcResponse(true, correlationId, Payload: element);
    }

    public static IpcResponse Ok(string correlationId)
    {
        return new IpcResponse(true, correlationId);
    }

    public static IpcResponse Fail(string correlationId, IpcErrorCode errorCode, string message)
    {
        return new IpcResponse(false, correlationId, errorCode, message);
    }
}
