using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace SecureDeviceControl.SnapshotPortal.Security;

public static class PortalSeal
{
    public const int MemorySizeKiB = 65_536;
    public const int Iterations = 3;
    public const int DegreeOfParallelism = 2;
    public const int HashLength = 32;

    // Argon2id seal only. The password itself is not stored in this project.
    private static readonly byte[] Salt = Convert.FromBase64String("qi6Yy9GetnkzMADTMmiO3Q==");
    private static readonly byte[] ExpectedHash = Convert.FromBase64String("J8vbSXuMV+py63G0bJRtbcmnjfAlFquYZwyyeYVWGZI=");

    public static bool Verify(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length > 128)
        {
            return false;
        }

        var computed = Hash(password, Salt);
        return CryptographicOperations.FixedTimeEquals(computed, ExpectedHash);
    }

    private static byte[] Hash(string password, byte[] salt)
    {
        var bytes = Encoding.UTF8.GetBytes(password);
        try
        {
            var argon2 = new Argon2id(bytes)
            {
                Salt = salt,
                MemorySize = MemorySizeKiB,
                Iterations = Iterations,
                DegreeOfParallelism = DegreeOfParallelism
            };

            return argon2.GetBytes(HashLength);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
