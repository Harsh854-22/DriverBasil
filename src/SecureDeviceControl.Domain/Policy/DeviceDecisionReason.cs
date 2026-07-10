namespace SecureDeviceControl.Domain.Policy;

public enum DeviceDecisionReason
{
    NotRemovableStorage,
    TrustedDevice,
    UnlockTimerActive,
    DefaultBlockUnknownRemovableStorage
}
