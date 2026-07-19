using SecureDeviceControl.Shared.Contracts;

namespace SecureDeviceControl.Domain.Devices;

public sealed record Device(
    string InstanceId,
    string StableId,
    DeviceClass DeviceClass,
    string FriendlyName,
    DeviceConnectionState ConnectionState,
    bool IsRemovableStorage);
