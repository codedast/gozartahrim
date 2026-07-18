using System.Net.Security;
using System.Security.Authentication;

namespace ServiceLib.Handler;

/// <summary>
/// Finds alternate front-IPs for an existing VLESS+Cloudflare profile:
/// samples IPs from Cloudflare's official IPv4 ranges and from FOFA search results,
/// then validates each candidate with a TCP + TLS handshake test.
/// Rewritten from the standalone Python prototype (v2raycode) onto v2rayN's own core/config plumbing.
/// </summary>
public class AltIpFinderHandler
{
    // Embedded default key so the feature works out of the box; never written to the user's
    // saved config unless they explicitly set a custom key in AltIpFinderItem.CustomFofaApiKey.
    private const string DefaultFofaApiKey = "e07547984526bca8f6716578e68e5f5d";
    private const string CloudflareIpv4Url = "https://www.cloudflare.com/ips-v4";
    private const string FofaSearchUrl = "https://fofa.info/api/v1/search/all";

    // cloudflare.com's WAF returns 403 for non-browser-looking User-Agents (e.g. the app name);
    // a normal browser UA is required for this specific request to succeed.
    private const string BrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

    private readonly Config _config;
    private readonly Random _random = new();

    public AltIpFinderHandler(Config config)
    {
        _config = config;
    }

    private AltIpFinderItem Settings => _config.AltIpFinderItem ??= new();

    public async Task<List<string>> FetchCloudflareIpv4RangesAsync()
    {
        var text = await DownloadTextAsync(CloudflareIpv4Url);
        var cidrs = (text ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.Contains('/'))
            .ToList();

        return SampleIpsFromCidrs(cidrs, Settings.SampleCount);
    }

    public static string BuildDefaultQuery(string countryCode) => $"\"cloudflare\" && country=\"{countryCode}\" && port=\"443\" && server==\"cloudflare\"";

    public async Task<List<string>> FetchFofaIpsAsync(string? countryCode = null, string? rawQuery = null)
    {
        var apiKey = Settings.CustomFofaApiKey.IsNullOrEmpty() ? DefaultFofaApiKey : Settings.CustomFofaApiKey!;
        var country = (countryCode.IsNullOrEmpty() ? Settings.FofaCountryCode : countryCode) ?? "US";
        var query = rawQuery.IsNotEmpty() ? rawQuery! : BuildDefaultQuery(country);
        var qbase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(query));
        var size = Settings.SampleCount;

        var url = $"{FofaSearchUrl}?key={apiKey}&qbase64={qbase64}&fields=ip,port&size={size}";

        var resp = await DownloadTextAsync(url);
        if (resp.IsNullOrEmpty())
        {
            return [];
        }

        try
        {
            var node = JsonNode.Parse(resp);
            if (node?["error"]?.GetValue<bool>() == true)
            {
                Logging.SaveLog("AltIpFinder", new Exception(node["errmsg"]?.ToString() ?? "FOFA request failed"));
                return [];
            }

            var results = node?["results"]?.AsArray();
            if (results == null)
            {
                return [];
            }

            return results
                .Select(r => r?[0]?.ToString())
                .Where(ip => ip.IsNotEmpty() && !ip!.Contains(':'))
                .Select(ip => ip!)
                .Distinct()
                .ToList();
        }
        catch (Exception ex)
        {
            Logging.SaveLog("AltIpFinder", ex);
            return [];
        }
    }

    /// <summary>
    /// Downloads via the app's local SOCKS proxy when a core is currently running and reachable,
    /// otherwise falls back to a direct connection. Both cloudflare.com and fofa.info are commonly
    /// blocked by ISP-level censorship, so users often need their own tunnel to reach them at all.
    /// </summary>
    private async Task<string?> DownloadTextAsync(string url)
    {
        var downloadService = new DownloadService();
        var viaProxy = await downloadService.TryDownloadString(url, true, BrowserUserAgent);
        if (viaProxy.IsNotEmpty())
        {
            return viaProxy;
        }

        return await downloadService.TryDownloadString(url, false, BrowserUserAgent);
    }

    public async Task<AltIpFinderResult> TestCandidateAsync(string ip, int port, string sni)
    {
        var result = new AltIpFinderResult { Ip = ip };
        var timeout = TimeSpan.FromMilliseconds(Settings.TestTimeoutMs);

        using var client = new System.Net.Sockets.TcpClient();
        var timer = Stopwatch.StartNew();
        try
        {
            using var connectCts = new CancellationTokenSource(timeout);
            await client.ConnectAsync(IPAddress.Parse(ip), port, connectCts.Token);
            result.TcpOk = true;
            result.LatencyMs = (int)timer.ElapsedMilliseconds;
        }
        catch (Exception)
        {
            return result;
        }

        try
        {
            using var sslStream = new SslStream(client.GetStream(), false, (_, _, _, _) => true);
            using var authCts = new CancellationTokenSource(timeout);
            await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = sni,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            }, authCts.Token);
            result.TlsOk = true;
        }
        catch (Exception)
        {
            // TCP reachable but TLS handshake with this SNI failed; still a usable-but-unverified candidate.
        }

        return result;
    }

    public async Task<List<AltIpFinderResult>> RunTestsAsync(
        IEnumerable<(string ip, EAltIpSource source)> candidates,
        int port,
        string sni,
        Func<AltIpFinderResult, Task>? onProgress,
        CancellationToken ct)
    {
        var results = new List<AltIpFinderResult>();
        var resultsLock = new object();
        using var semaphore = new SemaphoreSlim(Math.Max(1, Settings.TestConcurrency));
        var tasks = new List<Task>();

        foreach (var (ip, source) in candidates)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }
            await semaphore.WaitAsync(ct);
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }
                    var result = await TestCandidateAsync(ip, port, sni);
                    result.Source = source;
                    lock (resultsLock)
                    {
                        results.Add(result);
                    }
                    if (onProgress != null)
                    {
                        await onProgress(result);
                    }
                }
                catch (Exception ex)
                {
                    Logging.SaveLog("AltIpFinder", ex);
                }
                finally
                {
                    semaphore.Release();
                }
            }, ct));
        }

        await Task.WhenAll(tasks);
        return results;
    }

    /// <summary>
    /// Creates or reuses a local (non-remote) subscription group and clones the source profile
    /// into it once per valid candidate IP, leaving SNI/host untouched so TLS still targets the
    /// original domain.
    /// </summary>
    private static string SourceLabel(EAltIpSource source) => source switch
    {
        EAltIpSource.Cloudflare => "CL",
        EAltIpSource.Fofa => "F",
        _ => source.ToString(),
    };

    private static string ShortRemarks(string remarks)
    {
        var trimmed = remarks.Trim();
        return trimmed.Length <= 10 ? trimmed : trimmed[..10];
    }

    public async Task<(int Added, string? SubId)> AddValidCandidatesAsGroupAsync(ProfileItem source, List<AltIpFinderResult> validResults)
    {
        var validCandidates = validResults.Where(r => r.TcpOk).ToList();
        if (validCandidates.Count == 0)
        {
            return (0, null);
        }

        var existingSubs = await AppManager.Instance.SubItems();
        var maxSort = existingSubs?.LastOrDefault()?.Sort ?? 0;
        var subItem = new SubItem
        {
            Id = Utils.GetGuid(false),
            Remarks = $"{source.Remarks} - Alt IPs",
            Url = string.Empty,
            Enabled = true,
            Sort = maxSort + 1,
        };
        var addSubResult = await ConfigHandler.AddSubItem(_config, subItem);
        if (addSubResult != 0)
        {
            return (-1, null);
        }

        var added = 0;
        foreach (var candidate in validCandidates)
        {
            var clone = JsonUtils.DeepCopy(source);
            clone.IndexId = string.Empty;
            clone.Subid = subItem.Id;
            clone.IsSub = true;
            clone.Address = candidate.Ip;
            clone.Remarks = $"{ShortRemarks(source.Remarks)}{SourceLabel(candidate.Source)} [{candidate.Ip}]";

            if (clone.ConfigType == EConfigType.VLESS)
            {
                if (await ConfigHandler.AddVlessServer(_config, clone) == 0)
                {
                    added++;
                }
            }
        }

        return (added, subItem.Id);
    }

    private List<string> SampleIpsFromCidrs(List<string> cidrs, int count)
    {
        var result = new List<string>();
        if (cidrs.Count == 0 || count <= 0)
        {
            return result;
        }

        var attempts = 0;
        var maxAttempts = count * 10;
        while (result.Count < count && attempts < maxAttempts)
        {
            attempts++;
            var cidr = cidrs[_random.Next(cidrs.Count)];
            var ip = RandomIpInCidr(cidr);
            if (ip != null && !result.Contains(ip))
            {
                result.Add(ip);
            }
        }

        return result;
    }

    private string? RandomIpInCidr(string cidr)
    {
        var parts = cidr.Split('/');
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var baseAddress) || !int.TryParse(parts[1], out var prefixLen))
        {
            return null;
        }
        if (baseAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return null;
        }

        var baseBytes = baseAddress.GetAddressBytes();
        // Shift in the uint domain, not int: for octets >= 128 (e.g. 190.93.240.0/20),
        // shifting a promoted int sets the sign bit, and the checked (uint) cast on a
        // negative int throws OverflowException (this project builds with CheckForOverflowUnderflow=true).
        var baseUint = ((uint)baseBytes[0] << 24) | ((uint)baseBytes[1] << 16) | ((uint)baseBytes[2] << 8) | baseBytes[3];

        var hostBits = 32 - prefixLen;
        if (hostBits <= 0)
        {
            return baseAddress.ToString();
        }
        var hostCount = hostBits >= 32 ? uint.MaxValue : (1u << hostBits);

        var offset = hostCount <= 2 ? 0u : (uint)_random.NextInt64(1, hostCount - 1);
        var resultUint = baseUint + offset;
        var resultBytes = new[]
        {
            (byte)((resultUint >> 24) & 0xFF),
            (byte)((resultUint >> 16) & 0xFF),
            (byte)((resultUint >> 8) & 0xFF),
            (byte)(resultUint & 0xFF),
        };
        return new IPAddress(resultBytes).ToString();
    }
}
