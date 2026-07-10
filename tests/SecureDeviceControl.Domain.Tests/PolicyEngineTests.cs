using SecureDeviceControl.Domain.Devices;
using SecureDeviceControl.Domain.Policy;
using DevicePolicy = SecureDeviceControl.Domain.Policy.Policy;

namespace SecureDeviceControl.Domain.Tests;

public sealed class PolicyEngineTests
{
    private static readonly Device UnknownPendrive = new(
        "USBSTOR\\DISK&VEN_VENDOR&PROD_DRIVE\\001",
        "USBSTOR\\VENDOR_DRIVE_001",
        DeviceClass.RemovableStorage,
        "Vendor USB Drive",
        DeviceConnectionState.Connected,
        IsRemovableStorage: true);

    private readonly PolicyEngine policyEngine = new();

    [Fact]
    public void EvaluateBlocksUnknownRemovableStorageByDefault()
    {
        var decision = policyEngine.Evaluate(
            UnknownPendrive,
            DevicePolicy.Default,
            Array.Empty<TrustedDevice>(),
            UnlockTimer.Inactive,
            DateTimeOffset.Parse("2026-07-08T10:00:00Z"));

        Assert.Equal(DeviceDecisionAction.Block, decision.Action);
        Assert.Equal(DeviceDecisionReason.DefaultBlockUnknownRemovableStorage, decision.Reason);
    }

    [Fact]
    public void EvaluateAllowsTrustedRemovableStorage()
    {
        var trustedDevices = new[]
        {
            new TrustedDevice(UnknownPendrive.StableId, UnknownPendrive.FriendlyName, DateTimeOffset.UtcNow)
        };

        var decision = policyEngine.Evaluate(
            UnknownPendrive,
            DevicePolicy.Default,
            trustedDevices,
            UnlockTimer.Inactive,
            DateTimeOffset.Parse("2026-07-08T10:00:00Z"));

        Assert.Equal(DeviceDecisionAction.Allow, decision.Action);
        Assert.Equal(DeviceDecisionReason.TrustedDevice, decision.Reason);
    }

    [Fact]
    public void EvaluateAllowsUnknownRemovableStorageDuringUnlockTimer()
    {
        var now = DateTimeOffset.Parse("2026-07-08T10:00:00Z");
        var unlockTimer = new UnlockTimer(now.AddMinutes(15));

        var decision = policyEngine.Evaluate(
            UnknownPendrive,
            DevicePolicy.Default,
            Array.Empty<TrustedDevice>(),
            unlockTimer,
            now);

        Assert.Equal(DeviceDecisionAction.Allow, decision.Action);
        Assert.Equal(DeviceDecisionReason.UnlockTimerActive, decision.Reason);
    }
}
