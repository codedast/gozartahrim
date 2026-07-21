namespace ServiceLib.ViewModels;

/// <summary>
/// Local in-app message inbox (admin announcements + Telegram channel posts surfaced as toasts).
/// Backed by MessageStore; this is just a read/clear UI over it.
/// </summary>
public class MessagesViewModel : MyReactiveObject, ICloseable
{
    public event EventHandler? RequestClose;

    public Interaction<string, bool> ShowYesNoInteraction { get; } = new();

    public IObservableCollection<MessageDisplayItem> Messages { get; } = new ObservableCollectionExtended<MessageDisplayItem>();

    [Reactive] public bool IsEmpty { get; set; } = true;

    public ReactiveCommand<Unit, Unit> ClearAllCmd { get; }
    public ReactiveCommand<Unit, Unit> RefreshCmd { get; }
    public ReactiveCommand<Unit, Unit> CloseCmd { get; }
    public ReactiveCommand<string, Unit> OpenLinkCmd { get; }

    public MessagesViewModel()
    {
        _config = AppManager.Instance.Config;

        ClearAllCmd = ReactiveCommand.CreateFromTask(ClearAllAsync);
        RefreshCmd = ReactiveCommand.Create(Refresh);
        CloseCmd = ReactiveCommand.Create(() => { RequestClose?.Invoke(this, EventArgs.Empty); });
        OpenLinkCmd = ReactiveCommand.Create<string>(OpenLink);

        Refresh();
    }

    private void Refresh()
    {
        var items = MessageStore.Instance.All(_config).Select(ToDisplayItem).ToList();
        Messages.Clear();
        Messages.AddRange(items);
        IsEmpty = Messages.Count == 0;
    }

    private static MessageDisplayItem ToDisplayItem(GozarMessageItem item)
    {
        var safeLink = GozarLinks.SafeWeb(item.Link);
        string timeText;
        try
        {
            timeText = DateTimeOffset.FromUnixTimeSeconds(item.Ts).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        }
        catch
        {
            timeText = string.Empty;
        }

        var isAdmin = item.Source == EGozarMsgSource.Admin;
        return new MessageDisplayItem
        {
            SourceIsAdmin = isAdmin,
            SourceIsChannel = !isAdmin,
            SourceLabel = item.Source == EGozarMsgSource.Admin ? ResUI.MessagesSourceAdmin : ResUI.MessagesSourceChannel,
            Title = item.Title ?? string.Empty,
            HasTitle = item.Title.IsNotEmpty(),
            Body = item.Body ?? string.Empty,
            TimeText = timeText,
            Link = safeLink,
            HasLink = safeLink.IsNotEmpty(),
        };
    }

    private void OpenLink(string? link)
    {
        var safe = GozarLinks.SafeWeb(link);
        if (safe.IsNotEmpty())
        {
            ProcUtils.ProcessStart(safe);
        }
    }

    private async Task ClearAllAsync()
    {
        if (await ShowYesNoInteraction.Handle(ResUI.MessagesClearConfirm) == false)
        {
            return;
        }
        await MessageStore.Instance.Clear(_config);
        Refresh();
    }
}

public class MessageDisplayItem
{
    public bool SourceIsAdmin { get; set; }
    public bool SourceIsChannel { get; set; }
    public string SourceLabel { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool HasTitle { get; set; }
    public string Body { get; set; } = string.Empty;
    public string TimeText { get; set; } = string.Empty;
    public string? Link { get; set; }
    public bool HasLink { get; set; }
}
