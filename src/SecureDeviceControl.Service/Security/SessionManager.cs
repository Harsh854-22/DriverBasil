using System.Security.Cryptography;
using SecureDeviceControl.Shared.Security;
using DevicePolicy = SecureDeviceControl.Domain.Policy.Policy;

namespace SecureDeviceControl.Service;

public sealed class SessionManager
{
    private readonly TimeProvider timeProvider;
    private readonly Dictionary<string, SessionState> sessionsByHash = new();

    public SessionManager(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
    }

    public CreatedSession Create(PinPurpose purpose)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes);
        var tokenHash = HashToken(token);
        var expiresAt = timeProvider.GetUtcNow().Add(DevicePolicy.Default.SessionTimeout);

        lock (sessionsByHash)
        {
            sessionsByHash[tokenHash] = new SessionState(purpose, expiresAt);
        }

        return new CreatedSession(token, purpose, expiresAt);
    }

    public bool IsValid(string? token, PinPurpose? requiredPurpose = null)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var tokenHash = HashToken(token);
        lock (sessionsByHash)
        {
            if (!sessionsByHash.TryGetValue(tokenHash, out var session))
            {
                return false;
            }

            if (session.ExpiresAt <= timeProvider.GetUtcNow())
            {
                sessionsByHash.Remove(tokenHash);
                return false;
            }

            return requiredPurpose is null || session.Purpose == requiredPurpose;
        }
    }

    private static string HashToken(string token)
    {
        return Convert.ToBase64String(SHA256.HashData(Convert.FromBase64String(token)));
    }

    private sealed record SessionState(PinPurpose Purpose, DateTimeOffset ExpiresAt);
}

public sealed record CreatedSession(
    string Token,
    PinPurpose Purpose,
    DateTimeOffset ExpiresAt);
