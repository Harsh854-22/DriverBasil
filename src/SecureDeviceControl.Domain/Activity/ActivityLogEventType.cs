namespace SecureDeviceControl.Domain.Activity;

public enum ActivityLogEventType
{
    ServiceStarted,
    PinsInitialized,
    PinValidated,
    InvalidPin,
    DeviceUnlockStarted,
    DeviceLockApplied,
    UninstallAuthorizationIssued,
    IpcRejected,
    PolicyEvaluated,
    FileTransferDetected
}
