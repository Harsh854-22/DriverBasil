using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace SecureDeviceControl.Infrastructure.Security;

public sealed class Argon2idPinHasher : IPinHasher
{
    private const int SaltLength = 16;
    private const int HashLength = 32;
    private const int MemorySizeKiB = 65_536;
    private const int Iterations = 3;
    private const int DegreeOfParallelism = 2;

    public StoredPinCredential Hash(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var hash = Hash(pin, salt, MemorySizeKiB, Iterations, DegreeOfParallelism, HashLength);

        return new StoredPinCredential(
            salt,
            hash,
            MemorySizeKiB,
            Iterations,
            DegreeOfParallelism,
            HashLength);
    }

    public bool Verify(string pin, StoredPinCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);

        var computed = Hash(
            pin,
            credential.Salt,
            credential.MemorySizeKiB,
            credential.Iterations,
            credential.DegreeOfParallelism,
            credential.HashLength);

        return CryptographicOperations.FixedTimeEquals(computed, credential.Hash);
    }

    private static byte[] Hash(
        string pin,
        byte[] salt,
        int memorySizeKiB,
        int iterations,
        int degreeOfParallelism,
        int hashLength)
    {
        var pinBytes = Encoding.UTF8.GetBytes(pin);
        try
        {
            var argon2 = new Argon2id(pinBytes)
            {
                Salt = salt,
                MemorySize = memorySizeKiB,
                Iterations = iterations,
                DegreeOfParallelism = degreeOfParallelism
            };

            return argon2.GetBytes(hashLength);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pinBytes);
        }
    }
}
