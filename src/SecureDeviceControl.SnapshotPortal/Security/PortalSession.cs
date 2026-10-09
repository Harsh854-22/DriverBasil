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

    private readonly byte[] key;

    public SessionStore()
        : this(RandomNumberGenerator.GetBytes(32))
    {
    }

    public SessionStore(byte[] key)
    {
        if (key is null || key.Length < 32)
        {
            throw new ArgumentException("Session key must be at least 32 bytes.", nameof(key));
        }

        this.key = key;
    }

    public (string SessionId, PortalSession Session) Issue(string userAgent)
    {
        var session = new PortalSession
        {
            CsrfToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            UserAgentHash = Fingerprint(userAgent),
            CreatedAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow
        };

        return (Seal(session), session);
    }

    public PortalSession? Find(string? sessionId, string userAgent)
    {
        var session = Open(sessionId);
        if (session is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        if (now - session.CreatedAt > AbsoluteLifetime || now - session.LastSeenAt > IdleLifetime)
        {
            return null;
        }

        var presented = Convert.FromHexString(Fingerprint(userAgent));
        var bound = Convert.FromHexString(session.UserAgentHash);
        if (presented.Length != bound.Length || !CryptographicOperations.FixedTimeEquals(presented, bound))
        {
            return null;
        }

        session.LastSeenAt = now;
        return session;
    }

    public string Renew(PortalSession session) => Seal(session);

    public static string Fingerprint(string userAgent)
    {
        var material = Encoding.UTF8.GetBytes(userAgent ?? "");
        return Convert.ToHexString(SHA256.HashData(material));
    }

    private string Seal(PortalSession session)
    {
        var payload = string.Join(
            ".",
            session.CreatedAt.ToUnixTimeSeconds().ToString(),
            session.LastSeenAt.ToUnixTimeSeconds().ToString(),
            session.CsrfToken,
            session.UserAgentHash);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var signature = HMACSHA256.HashData(key, payloadBytes);
        return Base64Url(payloadBytes) + "." + Base64Url(signature);
    }

    private PortalSession? Open(string? ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket))
        {
            return null;
        }

        var parts = ticket.Split('.');
        if (parts.Length != 2)
        {
            return null;
        }

        byte[] payloadBytes;
        byte[] signature;
        try
        {
            payloadBytes = FromBase64Url(parts[0]);
            signature = FromBase64Url(parts[1]);
        }
        catch (FormatException)
        {
            return null;
        }

        var expected = HMACSHA256.HashData(key, payloadBytes);
        if (expected.Length != signature.Length || !CryptographicOperations.FixedTimeEquals(expected, signature))
        {
            return null;
        }

        var payload = Encoding.UTF8.GetString(payloadBytes).Split('.');
        if (payload.Length != 4 ||
            !long.TryParse(payload[0], out var created) ||
            !long.TryParse(payload[1], out var lastSeen) ||
            payload[2].Length != 64 ||
            payload[3].Length != 64)
        {
            return null;
        }

        return new PortalSession
        {
            CsrfToken = payload[2],
            UserAgentHash = payload[3],
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(created),
            LastSeenAt = DateTimeOffset.FromUnixTimeSeconds(lastSeen)
        };
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
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
