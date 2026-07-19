using Microsoft.Extensions.Logging.Abstractions;
using SecureDeviceControl.Infrastructure.Web;
using Xunit;

namespace SecureDeviceControl.Service.Tests;

public sealed class HostsWebFilterPolicyTests : IDisposable
{
    private readonly string tempHostsFile;
    private readonly HostsWebFilterPolicy policy;

    public HostsWebFilterPolicyTests()
    {
        tempHostsFile = Path.GetTempFileName();
        File.WriteAllLines(tempHostsFile, new[]
        {
            "127.0.0.1 localhost",
            "::1 localhost"
        });

        policy = new HostsWebFilterPolicy(NullLogger<HostsWebFilterPolicy>.Instance, tempHostsFile);
    }

    [Fact]
    public async Task ApplyWebFilterPolicyAsync_SelectiveAndRestrictedEmail_BlocksForbiddenWebsitesAndPersonalWebmail()
    {
        // Act: Selective mode allowing only google.com and mail.google.com, Restricted Email allowing gmail.com
        await policy.ApplyWebFilterPolicyAsync(
            WebFilterMode.Selective,
            new[] { "google.com", "mail.google.com" },
            Array.Empty<string>(),
            EmailFilterMode.Restricted,
            new[] { "gmail.com" },
            CancellationToken.None);

        var content = await File.ReadAllTextAsync(tempHostsFile);

        // Assert:
        Assert.Contains("# BEGIN SDC_WEB_FILTER", content);
        Assert.Contains("# END SDC_WEB_FILTER", content);
        Assert.Contains("127.0.0.1 facebook.com", content);
        Assert.Contains("127.0.0.1 yahoo.com", content);
        Assert.Contains("127.0.0.1 temp-mail.org", content);
        Assert.DoesNotContain("127.0.0.1 google.com\r", content);
        Assert.DoesNotContain("127.0.0.1 gmail.com\r", content);
    }

    [Fact]
    public async Task ApplyWebFilterPolicyAsync_Off_RemovesAllFilterRules()
    {
        // Apply rules first
        await policy.ApplyWebFilterPolicyAsync(
            WebFilterMode.Selective,
            new[] { "company.com" },
            Array.Empty<string>(),
            EmailFilterMode.Restricted,
            new[] { "company.com" },
            CancellationToken.None);

        // Turn OFF
        await policy.ApplyWebFilterPolicyAsync(
            WebFilterMode.Off,
            Array.Empty<string>(),
            Array.Empty<string>(),
            EmailFilterMode.Off,
            Array.Empty<string>(),
            CancellationToken.None);

        var content = await File.ReadAllTextAsync(tempHostsFile);

        Assert.DoesNotContain("# BEGIN SDC_WEB_FILTER", content);
        Assert.Contains("127.0.0.1 localhost", content);
    }

    public void Dispose()
    {
        if (File.Exists(tempHostsFile))
        {
            try { File.Delete(tempHostsFile); } catch { }
        }
    }
}
