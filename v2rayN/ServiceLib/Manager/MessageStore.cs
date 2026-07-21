namespace ServiceLib.Manager;

/// <summary>
/// A small local inbox for admin announcements and Telegram channel posts that were surfaced as
/// toast notifications, so the user can review them again later even after the toast has gone.
/// Persisted as part of Config; deduped by Key, newest-first, capped at 30 entries.
/// </summary>
public class MessageStore
{
    private static readonly Lazy<MessageStore> _instance = new(() => new());
    public static MessageStore Instance => _instance.Value;
    private static readonly string _tag = "MessageStore";
    private const int MaxItems = 30;
    private readonly object _lock = new();

    public async Task Add(Config config, GozarMessageItem item)
    {
        try
        {
            lock (_lock)
            {
                var list = config.GozarMessages ??= [];
                list.RemoveAll(t => t.Key == item.Key);
                list.Insert(0, item);
                if (list.Count > MaxItems)
                {
                    list.RemoveRange(MaxItems, list.Count - MaxItems);
                }
            }
            await ConfigHandler.SaveConfig(config);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    public List<GozarMessageItem> All(Config config)
    {
        return (config.GozarMessages ?? []).OrderByDescending(t => t.Ts).ToList();
    }

    public async Task Clear(Config config)
    {
        config.GozarMessages = [];
        await ConfigHandler.SaveConfig(config);
    }
}
