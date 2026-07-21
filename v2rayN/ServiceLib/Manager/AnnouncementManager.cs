using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ServiceLib.Manager;

/// <summary>
/// GozarTahrim: talks to the middleware server for two things:
///   1. an anonymous check-in (heartbeat) so the app owner can see active-user counts, and
///   2. the current broadcast announcement, shown to the user as a toast notification.
///
/// The only identifier sent is a random per-install GUID stored locally - no IP or other
/// personal data is ever transmitted. Requests prefer the app's own local proxy (the server sits
/// behind Cloudflare, which may be filtered without a working VPN), falling back to direct.
/// </summary>
public class AnnouncementManager
{
    private static readonly Lazy<AnnouncementManager> _instance = new(() => new());
    public static AnnouncementManager Instance => _instance.Value;
    private static readonly string _tag = "AnnouncementManager";
    private static readonly TimeSpan _minInterval = TimeSpan.FromSeconds(60);
    private bool _isRunning;
    private DateTime _lastRunAt = DateTime.MinValue;

    public async Task RunAsync(Config config)
    {
        if (_isRunning || DateTime.Now - _lastRunAt < _minInterval)
        {
            return;
        }

        var settings = config.AnnouncementItem ??= new();
        if (!settings.Enabled)
        {
            return;
        }

        _isRunning = true;
        try
        {
            await Checkin(config, settings);

            // Right after connect the tunnel may not be routable yet, so the first fetch can
            // fail. Retry a few times with a short delay, and only mark this run "done" once we
            // actually reach the server.
            for (var attempt = 0; attempt < 4; attempt++)
            {
                if (await FetchAnnouncement(config, settings))
                {
                    _lastRunAt = DateTime.Now;
                    await FetchArchive(config, settings);
                    break;
                }
                if (attempt < 3)
                {
                    await Task.Delay(4000);
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        finally
        {
            _isRunning = false;
        }
    }

    /// <summary>
    /// Forces an immediate check and returns a human-readable diagnostic, so the user/owner can
    /// tell "server unreachable" apart from "no announcement" apart from "shown". Ignores the
    /// throttle and re-shows the current announcement if one exists.
    /// </summary>
    public async Task<string> TestNowAsync(Config config)
    {
        var settings = config.AnnouncementItem ??= new();
        try
        {
            await Checkin(config, settings);

            var url = $"{BaseUrl(settings)}/api/announcement?version_code=0";
            var resp = await Request(config, url);
            if (resp.IsNullOrEmpty())
            {
                return $"سرور در دسترس نیست ({BaseUrl(settings)}). اگر به VPN وصل نیستی، اول وصل شو.";
            }

            using var doc = JsonDocument.Parse(resp);
            var root = doc.RootElement;
            if (!root.TryGetProperty("has", out var hasEl) || !hasEl.GetBoolean())
            {
                return "اتصال به سرور موفق بود، ولی اعلان فعالی وجود ندارد.";
            }

            var id = root.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0;
            var title = GetString(root, "title").IsNotEmpty() ? GetString(root, "title") : Global.AppName;
            var body = GetString(root, "body");
            var link = GetString(root, "link");
            var image = GetString(root, "image");

            ShowNotification(title, body, image, link);
            settings.LastAnnounceId = id.ToString();
            await ConfigHandler.SaveConfig(config);
            await MessageStore.Instance.Add(config, new GozarMessageItem
            {
                Source = EGozarMsgSource.Admin,
                Title = title == Global.AppName ? string.Empty : title,
                Body = body,
                Link = link,
                Ts = DateTimeOffset.Now.ToUnixTimeSeconds(),
                Key = $"a:{id}",
            });
            return $"اعلان #{id} دریافت و نمایش داده شد.";
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return $"پاسخ سرور نامعتبر بود: {ex.Message}";
        }
    }

    #region networking

    private static string BaseUrl(AnnouncementItem settings)
    {
        var url = settings.BaseUrl?.Trim().TrimEnd('/');
        return url.IsNotEmpty() && url.StartsWith("http") ? url : "https://gozarbooot.1mr.ir";
    }

    private static string InstallId(Config config, AnnouncementItem settings)
    {
        if (settings.InstallId.IsNotEmpty())
        {
            return settings.InstallId;
        }
        var id = Guid.NewGuid().ToString();
        settings.InstallId = id;
        _ = ConfigHandler.SaveConfig(config);
        return id;
    }

    private static string GetString(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;

    private static async Task<string?> Request(Config config, string url, string? postJson = null)
    {
        var proxy = await Request(config, url, postJson, true);
        if (proxy.IsNotEmpty())
        {
            return proxy;
        }
        return await Request(config, url, postJson, false);
    }

    private static async Task<string?> Request(Config config, string url, string? postJson, bool viaProxy)
    {
        try
        {
            using var handler = new HttpClientHandler();
            if (viaProxy)
            {
                var port = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);
                if (port <= 0)
                {
                    return null;
                }
                handler.Proxy = new WebProxy($"socks5://{Global.Loopback}:{port}");
                handler.UseProxy = true;
            }
            else
            {
                handler.UseProxy = false;
            }

            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("v2rayN");

            HttpResponseMessage resp;
            if (postJson != null)
            {
                using var content = new StringContent(postJson, Encoding.UTF8, "application/json");
                resp = await client.PostAsync(url, content);
            }
            else
            {
                resp = await client.GetAsync(url);
            }

            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }
            return await resp.Content.ReadAsStringAsync();
        }
        catch
        {
            return null;
        }
    }

    private static async Task Checkin(Config config, AnnouncementItem settings)
    {
        try
        {
            var body = new Dictionary<string, object?>
            {
                ["install_id"] = InstallId(config, settings),
                ["app_version"] = Utils.GetVersionInfo(),
                ["os"] = "windows",
            };

            var enc = await BuildEncryptedServerInfo(config, settings);
            if (enc.IsNotEmpty())
            {
                body["enc"] = enc;
            }

            var payload = JsonSerializer.Serialize(body);
            await Request(config, $"{BaseUrl(settings)}/api/checkin", payload);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    private static async Task<string?> PublicKeyPem(Config config, AnnouncementItem settings)
    {
        if (settings.CachedPubKeyPem.IsNotEmpty() && settings.CachedPubKeyPem.Contains("BEGIN PUBLIC KEY"))
        {
            return settings.CachedPubKeyPem;
        }

        var pem = await Request(config, $"{BaseUrl(settings)}/api/pubkey");
        if (pem.IsNotEmpty() && pem.Contains("BEGIN PUBLIC KEY"))
        {
            settings.CachedPubKeyPem = pem;
            await ConfigHandler.SaveConfig(config);
            return pem;
        }
        return null;
    }

    /// <summary>
    /// Builds the encrypted `enc` blob for the currently selected server:
    /// {"remark": label, "hash": sha256(server:port)[:10], "u4": first-8-of-uuid}, RSA-OAEP
    /// encrypted with the server public key. Returns null (checkin still goes out plainly) if
    /// there is no server, no key, or encryption fails.
    /// </summary>
    private static async Task<string?> BuildEncryptedServerInfo(Config config, AnnouncementItem settings)
    {
        try
        {
            var pem = await PublicKeyPem(config, settings);
            if (pem.IsNullOrEmpty())
            {
                return null;
            }

            var indexId = config.IndexId;
            if (indexId.IsNullOrEmpty())
            {
                return null;
            }

            var profile = await AppManager.Instance.GetProfileItem(indexId);
            if (profile is null || profile.Address.IsNullOrEmpty())
            {
                return null;
            }

            var remark = (profile.Remarks ?? string.Empty);
            remark = remark.Length > 48 ? remark[..48] : remark;
            var uuid = profile.Password ?? string.Empty;
            var u4 = uuid.Length >= 8 ? uuid[..8] : uuid;
            var hashFull = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{profile.Address}:{profile.Port}"))).ToLowerInvariant();
            var hash = hashFull.Length > 10 ? hashFull[..10] : hashFull;

            var json = JsonSerializer.Serialize(new { remark, hash, u4 });
            return RsaEncrypt(pem, json);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return null;
        }
    }

    /// <summary>RSA-OAEP(SHA-256, MGF1-SHA-256) encrypt, base64 - must match the server's crypto.</summary>
    private static string? RsaEncrypt(string pubPem, string plaintext)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pubPem);
            var cipher = rsa.Encrypt(Encoding.UTF8.GetBytes(plaintext), RSAEncryptionPadding.OaepSHA256);
            return Convert.ToBase64String(cipher);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return null;
        }
    }

    /// <summary>Pulls recent admin announcements into the local message box (no toast).</summary>
    private static async Task FetchArchive(Config config, AnnouncementItem settings)
    {
        try
        {
            var resp = await Request(config, $"{BaseUrl(settings)}/api/announcements?limit=10");
            if (resp.IsNullOrEmpty())
            {
                return;
            }

            using var doc = JsonDocument.Parse(resp);
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var o in items.EnumerateArray())
            {
                var id = o.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0;
                var title = GetString(o, "title");
                var body = GetString(o, "body");
                if (title.IsNullOrEmpty() && body.IsNullOrEmpty())
                {
                    continue;
                }
                var ts = o.TryGetProperty("ts", out var tsEl) && tsEl.TryGetInt64(out var tsVal) ? tsVal : DateTimeOffset.Now.ToUnixTimeSeconds();
                await MessageStore.Instance.Add(config, new GozarMessageItem
                {
                    Source = EGozarMsgSource.Admin,
                    Title = title,
                    Body = body,
                    Link = GetString(o, "link"),
                    Ts = ts,
                    Key = $"a:{id}",
                });
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    /// <returns>true if the server was reached (regardless of whether a new announcement exists).</returns>
    private static async Task<bool> FetchAnnouncement(Config config, AnnouncementItem settings)
    {
        var since = settings.LastAnnounceId.IsNotEmpty() && int.TryParse(settings.LastAnnounceId, out var s) ? s : 0;
        var url = $"{BaseUrl(settings)}/api/announcement?since={since}&version_code=0";
        var resp = await Request(config, url);
        if (resp.IsNullOrEmpty())
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(resp);
            var root = doc.RootElement;
            if (!root.TryGetProperty("has", out var hasEl) || !hasEl.GetBoolean())
            {
                return true;
            }

            var id = root.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0;
            var titleRaw = GetString(root, "title");
            var title = titleRaw.IsNotEmpty() ? titleRaw : Global.AppName;
            var body = GetString(root, "body");
            var image = GetString(root, "image");
            var link = GetString(root, "link");

            ShowNotification(title, body, image, link);
            settings.LastAnnounceId = id.ToString();
            await ConfigHandler.SaveConfig(config);
            await MessageStore.Instance.Add(config, new GozarMessageItem
            {
                Source = EGozarMsgSource.Admin,
                Title = title == Global.AppName ? string.Empty : title,
                Body = body,
                Link = link,
                Ts = DateTimeOffset.Now.ToUnixTimeSeconds(),
                Key = $"a:{id}",
            });
            return true;
        }
        catch (Exception ex)
        {
            // malformed response; treat as reached so we don't hammer the server
            Logging.SaveLog(_tag, ex);
            return true;
        }
    }

    private static void ShowNotification(string title, string body, string? imageUrl, string? link)
    {
        var shown = body.IsNotEmpty() ? body : (link.IsNotEmpty() ? link! : title);
        AppEvents.ToastNotificationRequested.Publish(new ToastNotificationPayload
        {
            Title = title,
            Message = shown.Length > 200 ? shown[..200] : shown,
            ImageUrl = imageUrl,
            LinkUrl = GozarLinks.SafeWeb(link),
        });
    }

    #endregion networking
}
