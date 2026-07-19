using SecureDeviceControl.Domain.Devices;
using SecureDeviceControl.Domain.Policy;
using SecureDeviceControl.Shared.Contracts;
using DevicePolicy = SecureDeviceControl.Domain.Policy.Policy;

namespace SecureDeviceControl.Domain.Tests;

public sealed class PolicyEngineStressTests
{
    [Fact]
    public void EvaluateHandlesLargePendriveDecisionVolume()
    {
        var policyEngine = new PolicyEngine();
        var now = DateTimeOffset.Parse("2026-07-09T10:00:00Z");
        var blockedCount = 0;

        for (var index = 0; index < 100_000; index++)
        {
            var device = new Device(
                $"USBSTOR\\DISK&VEN_VENDOR&PROD_DRIVE\\{index}",
                $"USBSTOR\\VENDOR_DRIVE_{index}",
                DeviceClass.RemovableStorage,
                $"Vendor USB Drive {index}",
                DeviceConnectionState.Connected,
                IsRemovableStorage: true);

            var decision = policyEngine.Evaluate(
                device,
                DevicePolicy.Default,
                Array.Empty<TrustedDevice>(),
                UnlockTimer.Inactive,
                now);

            if (decision.Action == DeviceDecisionAction.Block)
            {
                blockedCount++;
            }
        }

        Assert.Equal(100_000, blockedCount);
    }
}
