using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SecureDeviceControl.SnapshotPortal.Security;
using SecureDeviceControl.SnapshotPortal.Storage;

const string SessionCookieName = "__Host-sdc_session";
const int MaxBatchSnapshots = 500;

var hiddenSnapshotFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "DESKTOP-F80LOGH",
    "DESKTOP-PRAVIN",
    "DESKTOP-9397BI1"
};

var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var onPlatform = int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var platformPort);
var configuredSessionSecret = Environment.GetEnvironmentVariable("PORTAL_SESSION_SECRET");
if (onPlatform && (string.IsNullOrWhiteSpace(configuredSessionSecret) || configuredSessionSecret.Length < 32))
{
    throw new InvalidOperationException("Set PORTAL_SESSION_SECRET to a random string of at least 32 characters.");
}

var sessionKey = string.IsNullOrWhiteSpace(configuredSessionSecret)
    ? RandomNumberGenerator.GetBytes(32)
    : Encoding.UTF8.GetBytes(configuredSessionSecret);
var sessions = new SessionStore(sessionKey);
var throttle = new LoginThrottle();
var loginGate = new SemaphoreSlim(2);
var secrets = LocalDashboardSecrets.Load();
var store = new CloudSnapshotStore(secrets);
var libraryCache = new LibraryCache();

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseKestrel(options =>
{
    options.AddServerHeader = false;
    if (onPlatform)
    {
        options.ListenAnyIP(platformPort);
    }
    else
    {
        options.ListenLocalhost(7443, listen => listen.UseHttps());
    }
});

var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Items["sessions"] = sessions;
    await next();
});

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
    context.Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; img-src 'self'; style-src 'self'; script-src 'self'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'";
    context.Response.Headers["Cache-Control"] = "no-store";
    context.Response.Headers.Remove("Server");
    await next();
});

app.Use(async (context, next) =>
{
    if (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method))
    {
        if (!OriginAllowed(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
    }

    await next();
});

app.MapGet("/", () => Results.File(
    Path.Combine(app.Environment.WebRootPath, "index.html"),
    "text/html; charset=utf-8"));

app.MapGet("/dashboard", () => Results.File(
    Path.Combine(app.Environment.WebRootPath, "dashboard.html"),
    "text/html; charset=utf-8"));

app.MapPost("/api/login", async (HttpContext context) =>
{
    var clientKey = context.Connection.RemoteIpAddress?.ToString() ?? "local";
    if (throttle.IsLocked(clientKey, DateTimeOffset.UtcNow))
    {
        return Results.Json(new { ok = false, error = "Too many attempts. Wait 15 minutes." }, statusCode: StatusCodes.Status429TooManyRequests);
    }

    string password;
    try
    {
        password = await ReadPasswordAsync(context.Request, jsonOptions);
    }
    catch
    {
        return Results.Json(new { ok = false, error = "Access refused." }, statusCode: StatusCodes.Status400BadRequest);
    }

    var acquired = await loginGate.WaitAsync(TimeSpan.FromSeconds(2));
    if (!acquired)
    {
        return Results.Json(new { ok = false, error = "Access refused." }, statusCode: StatusCodes.Status429TooManyRequests);
    }

    bool accepted;
    try
    {
        accepted = PortalSeal.Verify(password);
    }
    finally
    {
        loginGate.Release();
    }

    if (!accepted)
    {
        throttle.RecordFailure(clientKey, DateTimeOffset.UtcNow);
        return Results.Json(new { ok = false, error = "Access refused." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    throttle.Reset(clientKey);
    var userAgent = context.Request.Headers.UserAgent.ToString();
    if (string.IsNullOrWhiteSpace(userAgent) || userAgent.Length > 512)
    {
        return Results.Json(new { ok = false, error = "Access refused." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var (sessionId, session) = sessions.Issue(userAgent);
    AppendSessionCookie(context, sessionId);
    return Results.Json(new { ok = true, csrf = session.CsrfToken });
});

app.MapPost("/api/logout", (HttpContext context) =>
{
    context.Response.Cookies.Delete(SessionCookieName, CookieOptions());
    return Results.Json(new { ok = true });
});

app.MapGet("/api/session", (HttpContext context) =>
{
    var session = CurrentSession(context);
    if (session is null)
    {
        return Results.Json(new { authenticated = false });
    }

    return Results.Json(new { authenticated = true, csrf = session.CsrfToken, cloudReady = store.IsConfigured });
});

app.MapGet("/api/storage", async (HttpContext context, CancellationToken cancellationToken) =>
{
    if (CurrentSession(context) is null)
    {
        return Results.Json(new { error = "Sign in required." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    if (!store.IsConfigured)
    {
        return Results.Json(new { error = "Cloud storage is not configured." }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        var usage = await store.GetUsageAsync(cancellationToken);
        return Results.Json(new
        {
            usedBytes = usage.UsedBytes,
            quotaBytes = usage.QuotaBytes,
            exact = usage.Exact
        });
    }
    catch (Exception)
    {
        return Results.Json(new { error = "Bucket size could not be read." }, statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapGet("/api/library", async (HttpContext context, CancellationToken cancellationToken) =>
{
    if (CurrentSession(context) is null)
    {
        return Results.Json(new { error = "Sign in required." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    if (!store.IsConfigured)
    {
        return Results.Json(new { error = "Add the Supabase service key in the desktop dashboard settings on this PC." }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        var frames = await libraryCache.GetOrLoadAsync(() => store.ListFramesAsync(cancellationToken), cancellationToken);
        var emails = await store.GetEmailByFolderAsync(cancellationToken);
        var devices = frames
            .Where(frame => !hiddenSnapshotFolders.Contains(frame.DeviceFolder))
            .GroupBy(frame => frame.DeviceFolder, StringComparer.OrdinalIgnoreCase)
            .Select(device => new
            {
                folder = device.Key,
                email = emails.TryGetValue(device.Key, out var registeredEmail) ? registeredEmail : "",
                count = device.Count(),
                days = device
                    .GroupBy(frame => frame.CapturedAtUtc.UtcDateTime.ToString("yyyy-MM-dd"))
                    .OrderByDescending(day => day.Key, StringComparer.Ordinal)
                    .Select(day => new
                    {
                        date = day.Key,
                        shots = day.OrderByDescending(frame => frame.CapturedAtUtc).Select(frame => new
                        {
                            key = frame.ObjectKey,
                            capturedAt = frame.CapturedAtUtc
                        })
                    })
            })
            .OrderBy(device => string.IsNullOrEmpty(device.email) ? device.folder : device.email, StringComparer.OrdinalIgnoreCase);

        return Results.Json(new { devices, total = frames.Count });
    }
    catch (Exception)
    {
        return Results.Json(new { error = "The cloud bucket could not be listed." }, statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapGet("/api/media", async (HttpContext context, string key, CancellationToken cancellationToken) =>
{
    if (CurrentSession(context) is null)
    {
        return Results.StatusCode(StatusCodes.Status401Unauthorized);
    }

    if (SnapshotCatalog.TryParse(key) is null)
    {
        return Results.StatusCode(StatusCodes.Status404NotFound);
    }

    try
    {
        var bytes = await store.DownloadAsync(key, cancellationToken);
        return Results.File(bytes, "image/jpeg");
    }
    catch (Exception)
    {
        return Results.StatusCode(StatusCodes.Status404NotFound);
    }
});

app.MapPost("/api/snapshots/delete", async (HttpContext context, CancellationToken cancellationToken) =>
{
    var session = CurrentSession(context);
    if (session is null)
    {
        return Results.Json(new { error = "Sign in required." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    DeleteRequest? body;
    try
    {
        body = await JsonSerializer.DeserializeAsync<DeleteRequest>(context.Request.Body, jsonOptions, cancellationToken);
    }
    catch
    {
        return Results.Json(new { error = "Could not delete that snapshot." }, statusCode: StatusCodes.Status400BadRequest);
    }

    if (body is null || !FixedEqual(body.Csrf, session.CsrfToken) || SnapshotCatalog.TryParse(body.Key) is null)
    {
        return Results.Json(new { error = "Could not delete that snapshot." }, statusCode: StatusCodes.Status400BadRequest);
    }

    try
    {
        await store.DeleteAsync(body.Key, cancellationToken);
        libraryCache.Invalidate();
        return Results.Json(new { ok = true });
    }
    catch (Exception)
    {
        return Results.Json(new { error = "The cloud bucket refused the delete." }, statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapPost("/api/snapshots/delete-batch", async (HttpContext context, CancellationToken cancellationToken) =>
{
    var session = CurrentSession(context);
    if (session is null)
    {
        return Results.Json(new { error = "Sign in required." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var (keys, error) = await ReadBatchKeysAsync(context, session, jsonOptions, cancellationToken);
    if (error is not null)
    {
        return error;
    }

    var batchKeys = keys!;
    try
    {
        var deleted = await store.DeleteManyAsync(batchKeys, cancellationToken);
        if (deleted > 0)
        {
            libraryCache.Invalidate();
        }

        return Results.Json(new
        {
            ok = deleted == batchKeys.Count,
            deleted,
            failed = batchKeys.Count - deleted
        });
    }
    catch (Exception)
    {
        return Results.Json(new { error = "The cloud bucket refused the delete." }, statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapPost("/api/snapshots/download-batch", async (HttpContext context, CancellationToken cancellationToken) =>
{
    var session = CurrentSession(context);
    if (session is null)
    {
        return Results.Json(new { error = "Sign in required." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    if (!store.IsConfigured)
    {
        return Results.Json(new { error = "Cloud storage is not configured." }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    var (keys, error) = await ReadBatchKeysAsync(context, session, jsonOptions, cancellationToken);
    if (error is not null)
    {
        return error;
    }

    var batchKeys = keys!;
    byte[] firstFrame;
    try
    {
        firstFrame = await store.DownloadAsync(batchKeys[0], cancellationToken);
    }
    catch (Exception)
    {
        return Results.Json(new { error = "The cloud bucket did not return those snapshots." }, statusCode: StatusCodes.Status502BadGateway);
    }

    var fileName = $"snapshots-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.zip";
    context.Response.ContentType = "application/zip";
    context.Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";

    using (var archive = new ZipArchive(context.Response.Body, ZipArchiveMode.Create, leaveOpen: true))
    {
        AddZipEntry(archive, batchKeys[0], firstFrame);
        foreach (var key in batchKeys.Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                AddZipEntry(archive, key, await store.DownloadAsync(key, cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // A frame removed while the zip is being built is skipped.
            }
        }
    }

    return Results.Empty;
});

app.UseStaticFiles();
app.Run();

static void AddZipEntry(ZipArchive archive, string objectKey, byte[] imageBytes)
{
    var entry = archive.CreateEntry(objectKey, CompressionLevel.NoCompression);
    using var entryStream = entry.Open();
    entryStream.Write(imageBytes, 0, imageBytes.Length);
}

static async Task<(List<string>? Keys, IResult? Error)> ReadBatchKeysAsync(
    HttpContext context,
    PortalSession session,
    JsonSerializerOptions jsonOptions,
    CancellationToken cancellationToken)
{
    if (context.Request.ContentLength is > 512 * 1024)
    {
        return (null, Results.Json(new { error = "That selection is too large." }, statusCode: StatusCodes.Status400BadRequest));
    }

    BatchRequest? body;
    try
    {
        body = await JsonSerializer.DeserializeAsync<BatchRequest>(context.Request.Body, jsonOptions, cancellationToken);
    }
    catch
    {
        return (null, Results.Json(new { error = "Could not read that selection." }, statusCode: StatusCodes.Status400BadRequest));
    }

    if (body is null || !FixedEqual(body.Csrf, session.CsrfToken))
    {
        return (null, Results.Json(new { error = "Could not read that selection." }, statusCode: StatusCodes.Status400BadRequest));
    }

    var keys = (body.Keys ?? new List<string>())
        .Where(key => SnapshotCatalog.TryParse(key) is not null)
        .Distinct(StringComparer.Ordinal)
        .ToList();

    if (keys.Count == 0)
    {
        return (null, Results.Json(new { error = "Select at least one snapshot." }, statusCode: StatusCodes.Status400BadRequest));
    }

    if (keys.Count > MaxBatchSnapshots)
    {
        return (null, Results.Json(
            new { error = $"Select no more than {MaxBatchSnapshots} snapshots at a time." },
            statusCode: StatusCodes.Status400BadRequest));
    }

    return (keys, null);
}

static async Task<string> ReadPasswordAsync(HttpRequest request, JsonSerializerOptions jsonOptions)
{
    if (request.ContentLength is > 4096)
    {
        throw new InvalidOperationException("Request is too large.");
    }

    request.EnableBuffering();
    if (request.HasFormContentType)
    {
        var form = await request.ReadFormAsync();
        return form["password"].ToString();
    }

    var document = await JsonSerializer.DeserializeAsync<LoginRequest>(request.Body, jsonOptions);
    return document?.Password ?? "";
}

static bool FixedEqual(string left, string right)
{
    var a = Encoding.UTF8.GetBytes(left ?? "");
    var b = Encoding.UTF8.GetBytes(right ?? "");
    return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
}

static bool OriginAllowed(HttpRequest request)
{
    var origin = request.Headers.Origin.ToString();
    if (string.IsNullOrEmpty(origin))
    {
        return true;
    }

    if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
    {
        return false;
    }

    var forwarded = request.Headers["X-Forwarded-Host"].ToString();
    var host = string.IsNullOrWhiteSpace(forwarded)
        ? request.Host.Host
        : forwarded.Split(',')[0].Trim().Split(':')[0];
    return originUri.Host.Equals(host, StringComparison.OrdinalIgnoreCase) ||
           originUri.Host.Equals(request.Host.Host, StringComparison.OrdinalIgnoreCase);
}

static PortalSession? CurrentSession(HttpContext context)
{
    var holder = context.Items["sessions"] as SessionStore;
    var session = holder?.Find(ReadSessionId(context), context.Request.Headers.UserAgent.ToString());
    if (holder is not null && session is not null)
    {
        AppendSessionCookie(context, holder.Renew(session));
    }

    return session;
}

static string? ReadSessionId(HttpContext context)
{
    return context.Request.Cookies.TryGetValue(SessionCookieName, out var value) ? value : null;
}

static void AppendSessionCookie(HttpContext context, string sessionId)
{
    context.Response.Cookies.Append(SessionCookieName, sessionId, CookieOptions());
}

static CookieOptions CookieOptions() => new()
{
    HttpOnly = true,
    Secure = true,
    SameSite = SameSiteMode.Strict,
    Path = "/",
    IsEssential = true,
    MaxAge = SessionStore.AbsoluteLifetime
};

public sealed class LibraryCache
{
    private readonly object gate = new();
    private IReadOnlyList<SnapshotFrame>? frames;
    private DateTimeOffset loadedAt;

    public async Task<IReadOnlyList<SnapshotFrame>> GetOrLoadAsync(
        Func<Task<IReadOnlyList<SnapshotFrame>>> load,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (frames is not null && DateTimeOffset.UtcNow - loadedAt < TimeSpan.FromSeconds(20))
            {
                return frames;
            }
        }

        var loaded = await load();
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            frames = loaded;
            loadedAt = DateTimeOffset.UtcNow;
            return frames;
        }
    }

    public void Invalidate()
    {
        lock (gate)
        {
            frames = null;
        }
    }
}

public sealed class LoginRequest
{
    public string Password { get; set; } = "";
}

public sealed class DeleteRequest
{
    public string Key { get; set; } = "";
    public string Csrf { get; set; } = "";
}

public sealed class BatchRequest
{
    public List<string>? Keys { get; set; }
    public string Csrf { get; set; } = "";
}
