using System.Security.Cryptography;
using System.Text;
using System.Runtime.Versioning;

namespace SecureDeviceControl.Infrastructure.Security;

[SupportedOSPlatform("windows")]
public sealed class DpapiSecretProtector : ISecretProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SecureDeviceControl.v1");

    public byte[] Protect(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        return ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.LocalMachine);
    }

    public byte[] Unprotect(byte[] protectedPayload)
    {
        ArgumentNullException.ThrowIfNull(protectedPayload);
        return ProtectedData.Unprotect(protectedPayload, Entropy, DataProtectionScope.LocalMachine);
    }
}
