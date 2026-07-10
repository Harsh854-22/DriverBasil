namespace SecureDeviceControl.Infrastructure.Security;

public interface ISecretProtector
{
    byte[] Protect(byte[] plaintext);

    byte[] Unprotect(byte[] protectedPayload);
}
