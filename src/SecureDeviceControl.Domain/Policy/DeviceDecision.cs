namespace SecureDeviceControl.Domain.Policy;

public sealed record DeviceDecision(
    DeviceDecisionAction Action,
    DeviceDecisionReason Reason);
