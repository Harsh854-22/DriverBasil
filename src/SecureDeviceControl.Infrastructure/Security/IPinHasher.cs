namespace SecureDeviceControl.Infrastructure.Security;

public interface IPinHasher
{
    StoredPinCredential Hash(string pin);

    bool Verify(string pin, StoredPinCredential credential);
}
