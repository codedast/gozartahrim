using System.Net;
using System.Text.Json;

namespace ServiceLib.Manager;

/// <summary>
/// Polls this fork's own GitHub releases and notifies (once per new tag) when a newer release
/// than Global.GtReleaseTag has been published, linking to the release page. Proxy-first then
/// direct, same pattern as TelegramChannelNotifyManager/AnnouncementManager.
/// </summary>
public class GozarUpdateChecker
{
    private static readonly Lazy<GozarUpdateChecker> _instance = new(() => new());
    public static GozarUpdateChecker Instance => _instance.Value;
    private static readonly string _tag = "GozarUpdateChecker";
    private static readonly TimeSpan _minInterval = TimeSpan.FromHours(6);
    private bool _isRunning;
    private DateTime _lastRunAt = DateTime.MinValue;

    public async Task CheckAsync(Config config)
    {
        if (_isRunning || DateTime.Now - _lastRunAt < _minInterval)
        {
            return;
        }

        _isRunning = true;
        try
        {
            var settings = config.AnnouncementItem ??= new();
            var url = $"https://api.github.com/repos/{Global.GtReleaseRepo}/releases/latest";
            var resp = await Request(url, true) ?? await Request(url, false);
            if (resp.IsNullOrEmpty())
            {
                return;
            }

            using var doc = JsonDocument.Parse(resp);
            var root = doc.RootElement;
            var tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() : null;
            var htmlUrl = root.TryGetProperty("html_url", out var urlEl) ? urlEl.GetString() : null;
            if (tag.IsNullOrEmpty())
            {
                return;
            }

            _lastRunAt = DateTime.Now;

            if (tag == Global.GtReleaseTag)
            {
                return;
            }
            if (tag == settings.UpdateLastNotifiedTag)
            {
                return;
            }

            var link = GozarLinks.SafeWeb(htmlUrl) ?? $"https://github.com/{Global.GtReleaseRepo}/releases/latest";
            AppEvents.ToastNotificationRequested.Publish(new ToastNotificationPayload
            {
                Title = Global.AppName,
                Message = $"نسخه جدید {tag} منتشر شد",
                LinkUrl = link,
            });

            settings.UpdateLastNotifiedTag = tag;
            await ConfigHandler.SaveConfig(config);
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

    private static async Task<string?> Request(string url, bool viaProxy)
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
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            var resp = await client.GetAsync(url);
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
}
