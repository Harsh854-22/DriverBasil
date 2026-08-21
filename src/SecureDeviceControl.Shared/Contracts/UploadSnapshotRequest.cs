namespace SecureDeviceControl.Shared.Contracts;

public sealed record UploadSnapshotRequest(
    byte[] ImageBytes,
    DateTimeOffset CapturedAt);
