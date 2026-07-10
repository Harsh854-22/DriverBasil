using SecureDeviceControl.Domain.Devices;

namespace SecureDeviceControl.Domain.Policy;

public sealed class PolicyEngine
{
    public DeviceDecision Evaluate(
        Device device,
        Policy policy,
        IReadOnlyCollection<TrustedDevice> trustedDevices,
        UnlockTimer unlockTimer,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(trustedDevices);
        ArgumentNullException.ThrowIfNull(unlockTimer);

        if (!device.IsRemovableStorage)
        {
            return new DeviceDecision(DeviceDecisionAction.Ignore, DeviceDecisionReason.NotRemovableStorage);
        }

        if (trustedDevices.Any(trustedDevice => trustedDevice.StableId == device.StableId))
        {
            return new DeviceDecision(DeviceDecisionAction.Allow, DeviceDecisionReason.TrustedDevice);
        }

        if (unlockTimer.IsActiveAt(now))
        {
            return new DeviceDecision(DeviceDecisionAction.Allow, DeviceDecisionReason.UnlockTimerActive);
        }

        return policy.Mode switch
        {
            PolicyMode.BlockUnknownRemovableStorage => new DeviceDecision(
                DeviceDecisionAction.Block,
                DeviceDecisionReason.DefaultBlockUnknownRemovableStorage),
            _ => new DeviceDecision(DeviceDecisionAction.Block, DeviceDecisionReason.DefaultBlockUnknownRemovableStorage)
        };
    }
}
