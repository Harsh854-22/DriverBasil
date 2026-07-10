namespace SecureDeviceControl.Shared.Ipc;

public enum IpcErrorCode
{
    None = 0,
    BadRequest,
    Unauthorized,
    InvalidPin,
    RateLimited,
    SessionExpired,
    NotInitialized,
    AlreadyInitialized,
    PolicyDenied,
    ServiceUnavailable,
    InternalError
}
