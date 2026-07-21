namespace ServiceLib.Manager;

/// <summary>
/// Polls the public web preview of a Telegram channel (https://t.me/s/&lt;channel&gt;) for new posts
/// and raises AppEvents.ToastNotificationRequested so each platform UI can show a native
/// notification. No bot token is embedded in the app (would be extractable by any user), and no
/// backend server is required - the channel's own public preview page is the transport.
/// </summary>
public class TelegramChannelNotifyManager
{
    private static readonly Lazy<TelegramChannelNotifyManager> _instance = new(() => new());
    public static TelegramChannelNotifyManager Instance => _instance.Value;
    private static readonly string _tag = "TelegramChannelNotifyManager";
    private static readonly TimeSpan _minInterval = TimeSpan.FromSeconds(60);
    private bool _isRunning;
    private DateTime _lastCheckAt = DateTime.MinValue;

    private static readonly Regex _postRegex = new(
        @"data-post=""(?<post>[^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex _textRegex = new(
        @"tgme_widget_message_text[^>]*>(?<text>.*?)</div>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex _photoRegex = new(
        @"tgme_widget_message_photo_wrap[^""]*""[^>]*style=""[^""]*background-image:url\('(?<img>[^']+)'\)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex _linkRegex = new(
        @"<a[^>]+href=""(?<href>https?://[^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex _tagRegex = new("<[^>]+>", RegexOptions.Compiled);

    public async Task CheckForNewPostAsync(Config config)
    {
        if (_isRunning || DateTime.Now - _lastCheckAt < _minInterval)
        {
            return;
        }

        var settings = config.TelegramNotifyItem ??= new();
        if (!settings.Enabled || settings.ChannelUsername.IsNullOrEmpty())
        {
            return;
        }

        _isRunning = true;
        _lastCheckAt = DateTime.Now;
        try
        {
            var url = $"https://t.me/s/{settings.ChannelUsername}";
            var downloadService = new DownloadService();
            // Prefer the app's own local proxy (only meaningful once the user is connected to a
            // working server), fall back to direct - Telegram is filtered in Iran, so this only
            // succeeds once a VPN connection is actually up; that's fine, it's retried every tick.
            var html = await downloadService.TryDownloadString(url, true, string.Empty);
            if (html.IsNullOrEmpty())
            {
                html = await downloadService.TryDownloadString(url, false, string.Empty);
            }
            if (html.IsNullOrEmpty())
            {
                Logging.SaveLog($"{_tag}: fetch failed (no proxy/direct access to {url})");
                return;
            }

            var postMatches = _postRegex.Matches(html);
            if (postMatches.Count == 0)
            {
                Logging.SaveLog($"{_tag}: fetch ok but no messages found on the page (history may be hidden)");
                return;
            }

            var lastMatch = postMatches[^1];
            var postId = lastMatch.Groups["post"].Value;
            Logging.SaveLog($"{_tag}: latest post on page = {postId}, last seen = {settings.LastMessagePostId ?? "(none)"}");
            if (postId == settings.LastMessagePostId)
            {
                return;
            }

            var blockStart = lastMatch.Index;
            var block = html[blockStart..];

            var textMatch = _textRegex.Match(block);
            var messageHtml = textMatch.Success ? textMatch.Groups["text"].Value : string.Empty;
            var messageText = WebUtility.HtmlDecode(_tagRegex.Replace(messageHtml, string.Empty)).Trim();

            var photoMatch = _photoRegex.Match(block);
            var imageUrl = photoMatch.Success ? photoMatch.Groups["img"].Value : null;

            var linkMatch = _linkRegex.Matches(messageHtml)
                .Select(m => m.Groups["href"].Value)
                .FirstOrDefault(href => !href.Contains("t.me/") || href.Contains($"t.me/{settings.ChannelUsername}/"));

            settings.LastMessagePostId = postId;
            await ConfigHandler.SaveConfig(config);

            if (messageText.IsNullOrEmpty() && imageUrl.IsNullOrEmpty())
            {
                Logging.SaveLog($"{_tag}: new post {postId} had neither text nor image, skipping notification");
                return;
            }

            Logging.SaveLog($"{_tag}: publishing notification for {postId}");
            var link = linkMatch ?? $"https://t.me/{settings.ChannelUsername}/{postId.Split('/').LastOrDefault()}";
            AppEvents.ToastNotificationRequested.Publish(new ToastNotificationPayload
            {
                Title = Global.AppName,
                Message = messageText.IsNotEmpty() ? messageText : "پیام جدید در کانال",
                ImageUrl = imageUrl,
                LinkUrl = link,
            });

            await MessageStore.Instance.Add(config, new GozarMessageItem
            {
                Source = EGozarMsgSource.Channel,
                Title = Global.AppName,
                Body = messageText.IsNotEmpty() ? messageText : "پیام جدید در کانال",
                Link = link,
                Ts = DateTimeOffset.Now.ToUnixTimeSeconds(),
                Key = $"c:{postId}",
            });
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
}
