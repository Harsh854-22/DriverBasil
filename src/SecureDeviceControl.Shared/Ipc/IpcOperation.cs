namespace SecureDeviceControl.Shared.Ipc;

public enum IpcOperation
{
    GetServiceStatus,
    InitializePins,
    ValidatePin,
    StartUnlockTimer,
    RequestUninstallAuthorization,
    ListActivityLogs
}
