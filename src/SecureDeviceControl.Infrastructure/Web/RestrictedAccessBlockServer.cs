using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;

namespace SecureDeviceControl.Infrastructure.Web;

public sealed class RestrictedAccessBlockServer : IDisposable
{
    private readonly ILogger<RestrictedAccessBlockServer> logger;
    private HttpListener? listener;
    private CancellationTokenSource? cts;
    private string userEmail = "";
    private string machineName = Environment.MachineName;

    public RestrictedAccessBlockServer(ILogger<RestrictedAccessBlockServer> logger)
    {
        this.logger = logger;
    }

    public void UpdateUserContext(string email, string machine)
    {
        if (!string.IsNullOrWhiteSpace(email)) userEmail = email;
        if (!string.IsNullOrWhiteSpace(machine)) machineName = machine;
    }

    public void StartServer(int port = 8085)
    {
        if (listener != null && listener.IsListening) return;

        try
        {
            listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();

            cts = new CancellationTokenSource();
            _ = ListenAsync(cts.Token);
            logger.LogInformation("Restricted Access Warning Page server started on port {Port}.", port);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not start local Restricted Access Block HTTP server.");
        }
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && listener != null && listener.IsListening)
        {
            try
            {
                var context = await listener.GetContextAsync();
                _ = ProcessRequestAsync(context);
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Error accepting block page request.");
            }
        }
    }

    private async Task ProcessRequestAsync(HttpListenerContext context)
    {
        try
        {
            var response = context.Response;
            response.ContentType = "text/html; charset=utf-8";
            response.StatusCode = 403; // Forbidden

            var html = RenderBlockPageHtml(userEmail, machineName);
            var buffer = Encoding.UTF8.GetBytes(html);

            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
        catch
        {
            // Ignore response errors
        }
    }

    public static string RenderBlockPageHtml(string email, string machine)
    {
        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="UTF-8">
              <meta name="viewport" content="width=device-width, initial-scale=1.0">
              <title>Access Restricted - Corporate Security Policy</title>
              <style>
                body {
                  font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, sans-serif;
                  background-color: #0F172A;
                  color: #F8FAFC;
                  display: flex;
                  align-items: center;
                  justify-content: center;
                  min-height: 100vh;
                  margin: 0;
                  padding: 20px;
                  box-sizing: border-box;
                }
                .card {
                  background-color: #1E293B;
                  border: 1px solid #334155;
                  border-radius: 16px;
                  padding: 40px;
                  max-width: 540px;
                  text-align: center;
                  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.4);
                }
                .icon {
                  font-size: 56px;
                  margin-bottom: 16px;
                }
                h1 {
                  font-size: 26px;
                  font-weight: 700;
                  color: #EF4444;
                  margin-bottom: 12px;
                }
                p {
                  color: #94A3B8;
                  font-size: 15px;
                  line-height: 1.6;
                  margin-bottom: 24px;
                }
                .callout {
                  background-color: rgba(239, 68, 68, 0.1);
                  border: 1px solid rgba(239, 68, 68, 0.3);
                  border-radius: 10px;
                  padding: 16px;
                  color: #FCA5A5;
                  font-weight: 600;
                  font-size: 15px;
                  margin-bottom: 24px;
                }
                .meta {
                  font-size: 13px;
                  color: #64748B;
                  border-top: 1px solid #334155;
                  padding-top: 16px;
                }
              </style>
            </head>
            <body>
              <div class="card">
                <div class="icon">🔒</div>
                <h1>Access Restricted</h1>
                <p>This website, email service, or network connection has been restricted by your organization's security policy.</p>
                <div class="callout">
                  Kindly contact your IT Administrator to request access.
                </div>
                <div class="meta">
                  PC: <strong>{{machine}}</strong> &nbsp;|&nbsp; User: <strong>{{(string.IsNullOrWhiteSpace(email) ? "Registered PC" : email)}}</strong>
                </div>
              </div>
            </body>
            </html>
            """;
    }

    public void Dispose()
    {
        cts?.Cancel();
        try { listener?.Stop(); } catch { }
        try { listener?.Close(); } catch { }
    }
}
