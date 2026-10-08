using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SecureDeviceControl.SnapshotPortal.Security;

public sealed class PortalSession
{
    public required string CsrfToken { get; init; }
    public required string UserAgentHash { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset LastSeenAt { get; set; }
}

public sealed class SessionStore
{
    public static readonly TimeSpan IdleLifetime = TimeSpan.FromMinutes(20);
    public static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromHours(8);

    private readonly ConcurrentDictionary<string, PortalSession> sessions = new();

    public (string SessionId, PortalSession Session) Issue(string userAgent)
    {
        var sessionId = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var session = new PortalSession
        {
            CsrfToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            UserAgentHash = Fingerprint(userAgent),
            CreatedAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow
        };

        sessions[Key(sessionId)] = session;
        return (sessionId, session);
    }

    public PortalSession? Find(string? sessionId, string userAgent)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || sessionId.Length != 64)
        {
            return null;
        }

        if (!sessions.TryGetValue(Key(sessionId), out var session))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        if (now - session.CreatedAt > AbsoluteLifetime || now - session.LastSeenAt > IdleLifetime)
        {
            sessions.TryRemove(Key(sessionId), out _);
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(session.UserAgentHash),
                Convert.FromHexString(Fingerprint(userAgent))))
        {
            sessions.TryRemove(Key(sessionId), out _);
            return null;
        }

        session.LastSeenAt = now;
        return session;
    }

    public void Revoke(string? sessionId)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            sessions.TryRemove(Key(sessionId), out _);
        }
    }

    public static string Fingerprint(string userAgent)
    {
        var material = Encoding.UTF8.GetBytes(userAgent ?? "");
        return Convert.ToHexString(SHA256.HashData(material));
    }

    private static string Key(string sessionId) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sessionId)));
}

public sealed class LoginThrottle
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private const int MaxFailures = 5;
    private readonly ConcurrentDictionary<string, AttemptWindow> attempts = new();

    public bool IsLocked(string clientKey, DateTimeOffset now)
    {
        if (!attempts.TryGetValue(clientKey, out var window))
        {
            return false;
        }

        if (now - window.StartedAt > Window)
        {
            attempts.TryRemove(clientKey, out _);
            return false;
        }

        return window.Failures >= MaxFailures;
    }

    public void RecordFailure(string clientKey, DateTimeOffset now)
    {
        attempts.AddOrUpdate(
            clientKey,
            _ => new AttemptWindow(now, 1),
            (_, existing) => now - existing.StartedAt > Window
                ? new AttemptWindow(now, 1)
                : existing with { Failures = existing.Failures + 1 });
    }

    public void Reset(string clientKey) => attempts.TryRemove(clientKey, out _);

    private sealed record AttemptWindow(DateTimeOffset StartedAt, int Failures);
}
