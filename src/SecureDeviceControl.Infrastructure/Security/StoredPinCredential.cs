namespace SecureDeviceControl.Infrastructure.Security;

public sealed record StoredPinCredential(
    byte[] Salt,
    byte[] Hash,
    int MemorySizeKiB,
    int Iterations,
    int DegreeOfParallelism,
    int HashLength);
