using Microsoft.Extensions.Logging;

namespace SecureDeviceControl.Infrastructure.Web;

public sealed class HostsWebFilterPolicy : IWebFilterPolicy
{
    private const string BeginMarker = "# BEGIN SDC_WEB_FILTER";
    private const string EndMarker = "# END SDC_WEB_FILTER";

    private static readonly string SystemHostsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "drivers", "etc", "hosts");

    private static readonly string[] StandardPersonalWebmails =
    {
        "gmail.com", "www.gmail.com",
        "mail.google.com",
        "yahoo.com", "mail.yahoo.com",
        "outlook.live.com", "hotmail.com",
        "mail.ru", "temp-mail.org", "yopmail.com"
    };

    private static readonly string[] StandardDistractingWebsites =
    {
        "facebook.com", "www.facebook.com",
        "instagram.com", "www.instagram.com",
        "twitter.com", "www.twitter.com", "x.com", "www.x.com",
        "tiktok.com", "www.tiktok.com",
        "reddit.com", "www.reddit.com", "youtube.com", "www.youtube.com"
    };

    private readonly string hostsFilePath;
    private readonly ILogger<HostsWebFilterPolicy> logger;

    public HostsWebFilterPolicy(ILogger<HostsWebFilterPolicy> logger, string? customHostsPath = null)
    {
        this.logger = logger;
        this.hostsFilePath = customHostsPath ?? SystemHostsPath;
    }

    public async Task ApplyWebFilterPolicyAsync(
        WebFilterMode mode,
        IReadOnlyList<string> allowedWebsites,
        IReadOnlyList<string> blockedWebsites,
        EmailFilterMode emailMode,
        IReadOnlyList<string> allowedEmailDomains,
        CancellationToken cancellationToken)
    {
        try
        {
            var blockedDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Handle Web Filter Mode
            if (mode == WebFilterMode.Blocklist)
            {
                foreach (var site in blockedWebsites)
                {
                    AddDomainWithVariants(blockedDomains, site);
                }
            }
            else if (mode == WebFilterMode.Selective)
            {
                var allowedSet = new HashSet<string>(allowedWebsites.Select(CleanDomain), StringComparer.OrdinalIgnoreCase);

                // In Selective mode, block standard distracting sites unless explicitly in allowed list
                foreach (var site in StandardDistractingWebsites)
                {
                    if (!allowedSet.Contains(CleanDomain(site)))
                    {
                        blockedDomains.Add(site);
                    }
                }
            }

            // 2. Handle Email Filter Mode
            if (emailMode == EmailFilterMode.Restricted)
            {
                var allowedEmailSet = new HashSet<string>(allowedEmailDomains.Select(CleanDomain), StringComparer.OrdinalIgnoreCase);

                foreach (var webmail in StandardPersonalWebmails)
                {
                    var cleanWebmail = CleanDomain(webmail);
                    bool isAllowed = false;
                    foreach (var allowed in allowedEmailSet)
                    {
                        if (cleanWebmail.Equals(allowed, StringComparison.OrdinalIgnoreCase) ||
                            cleanWebmail.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase) ||
                            (allowed.Equals("gmail.com", StringComparison.OrdinalIgnoreCase) && (cleanWebmail.Contains("gmail") || cleanWebmail.Contains("mail.google.com"))))
                        {
                            isAllowed = true;
                            break;
                        }
                    }

                    if (!isAllowed)
                    {
                        blockedDomains.Add(webmail);
                    }
                }
            }

            // 3. Write Hosts Redirect Block Entries
            await ApplyHostsFileBlockListAsync(blockedDomains, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply system web filter policy.");
        }
    }

    private async Task ApplyHostsFileBlockListAsync(HashSet<string> blockedDomains, CancellationToken cancellationToken)
    {
        if (!File.Exists(hostsFilePath))
        {
            logger.LogWarning("Hosts file not found at '{Path}'. Skipping hosts filter.", hostsFilePath);
            return;
        }

        var lines = await File.ReadAllLinesAsync(hostsFilePath, cancellationToken);
        var filteredLines = new List<string>();
        var insideBlock = false;

        foreach (var line in lines)
        {
            if (line.Trim() == BeginMarker)
            {
                insideBlock = true;
                continue;
            }

            if (line.Trim() == EndMarker)
            {
                insideBlock = false;
                continue;
            }

            if (!insideBlock)
            {
                filteredLines.Add(line);
            }
        }

        if (blockedDomains.Count > 0)
        {
            filteredLines.Add(BeginMarker);
            filteredLines.Add("# Automated Security Policy Web Block Rules");
            foreach (var domain in blockedDomains.OrderBy(d => d))
            {
                filteredLines.Add($"127.0.0.1 {domain}");
                filteredLines.Add($"127.0.0.1 www.{CleanDomain(domain)}");
            }
            filteredLines.Add(EndMarker);
        }

        await File.WriteAllLinesAsync(hostsFilePath, filteredLines, cancellationToken);
        logger.LogInformation("Applied web filter policy with {Count} blocked domain rules.", blockedDomains.Count);
    }

    private static void AddDomainWithVariants(HashSet<string> set, string rawDomain)
    {
        var domain = CleanDomain(rawDomain);
        if (string.IsNullOrWhiteSpace(domain)) return;

        set.Add(domain);
        if (!domain.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            set.Add("www." + domain);
        }
    }

    private static string CleanDomain(string domain)
    {
        return domain.Replace("https://", "", StringComparison.OrdinalIgnoreCase)
                     .Replace("http://", "", StringComparison.OrdinalIgnoreCase)
                     .Trim('/', ' ');
    }
}
