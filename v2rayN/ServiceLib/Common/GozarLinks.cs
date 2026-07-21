namespace ServiceLib.Common;

/// <summary>
/// Security hardening for remote-supplied links (announcement link, Telegram post link, etc.):
/// only http(s) URLs are ever opened via the OS shell, blocking javascript:/file:/intent:-style
/// schemes that a malicious or compromised backend could otherwise use.
/// </summary>
public static class GozarLinks
{
    public static string? SafeWeb(string? url)
    {
        if (url.IsNullOrEmpty())
        {
            return null;
        }

        var trimmed = url.Trim();
        return trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : null;
    }
}
