using SecureDeviceControl.Shared.Ipc;

namespace SecureDeviceControl.Service.Ipc;

public sealed class IpcRequestException : Exception
{
    public IpcRequestException(IpcErrorCode errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public IpcErrorCode ErrorCode { get; }
}
