using System.Reactive.Concurrency;

namespace ServiceLib.ViewModels;

public class AltIpFinderViewModel : MyReactiveObject, ICloseable
{
    public event EventHandler? RequestClose;

    private readonly ProfileItem _source;
    private CancellationTokenSource? _cts;

    [Reactive] public bool IsRunning { get; set; }
    [Reactive] public string ProgressText { get; set; } = string.Empty;
    [Reactive] public string? CustomFofaApiKey { get; set; }
    [Reactive] public string CountryCode { get; set; } = "IR";
    [Reactive] public int SampleCount { get; set; } = 30;
    [Reactive] public int TestConcurrency { get; set; } = 20;
    [Reactive] public FofaCountryPreset? SelectedPreset { get; set; }
    [Reactive] public string Query { get; set; } = string.Empty;

    public List<FofaCountryPreset> Presets { get; } = FofaCountryPresets.All;

    public IObservableCollection<AltIpFinderResult> Results { get; } = new ObservableCollectionExtended<AltIpFinderResult>();
    public string? CreatedSubId { get; private set; }

    public ReactiveCommand<Unit, Unit> StartCmd { get; }
    public ReactiveCommand<Unit, Unit> CancelCmd { get; }
    public ReactiveCommand<Unit, Unit> CloseCmd { get; }

    public AltIpFinderViewModel(ProfileItem source)
    {
        _config = AppManager.Instance.Config;
        _source = source;

        var settings = _config.AltIpFinderItem ??= new();
        CustomFofaApiKey = settings.CustomFofaApiKey;
        CountryCode = settings.FofaCountryCode ?? "IR";
        SampleCount = settings.SampleCount;
        TestConcurrency = settings.TestConcurrency;
        SelectedPreset = FofaCountryPresets.FindByCode(CountryCode) ?? Presets.First();
        Query = settings.CustomQuery.IsNotEmpty() ? settings.CustomQuery! : AltIpFinderHandler.BuildDefaultQuery(CountryCode);

        this.WhenAnyValue(x => x.SelectedPreset)
            .Skip(1)
            .Subscribe(preset =>
            {
                if (preset is null)
                {
                    return;
                }
                CountryCode = preset.Code;
                Query = AltIpFinderHandler.BuildDefaultQuery(preset.Code);
            });

        StartCmd = ReactiveCommand.CreateFromTask(RunAsync, this.WhenAnyValue(x => x.IsRunning, running => !running));
        CancelCmd = ReactiveCommand.Create(() => { _cts?.Cancel(); }, this.WhenAnyValue(x => x.IsRunning));
        CloseCmd = ReactiveCommand.Create(() => { RequestClose?.Invoke(this, EventArgs.Empty); });

        // Defense in depth: RunAsync already catches its own exceptions, but this guarantees
        // the app can never crash from this dialog even if an unexpected exception slips through.
        StartCmd.ThrownExceptions.Subscribe(ex =>
        {
            Logging.SaveLog("AltIpFinder", ex);
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
            IsRunning = false;
        });
    }

    private async Task RunAsync()
    {
        if (_source.ConfigType != EConfigType.VLESS)
        {
            NoticeManager.Instance.Enqueue(ResUI.AltIpFinderOnlyVless);
            return;
        }

        await SaveSettingsAsync();

        IsRunning = true;
        Results.Clear();
        ProgressText = string.Empty;
        _cts = new CancellationTokenSource();

        try
        {
            var handler = new AltIpFinderHandler(_config);

            ProgressText = "Fetching candidate IPs...";
            var cfIps = await handler.FetchCloudflareIpv4RangesAsync();
            var fofaIps = await handler.FetchFofaIpsAsync(CountryCode, Query);

            var candidates = cfIps.Select(ip => (ip, EAltIpSource.Cloudflare))
                .Concat(fofaIps.Select(ip => (ip, EAltIpSource.Fofa)))
                .ToList();

            if (candidates.Count == 0)
            {
                NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
                return;
            }

            var tested = 0;
            var total = candidates.Count;
            var sni = _source.Sni.IsNullOrEmpty() ? _source.Address : _source.Sni;

            var results = await handler.RunTestsAsync(candidates, _source.Port, sni, async result =>
            {
                // RunTestsAsync invokes this callback concurrently from background worker threads;
                // Results is bound to a UI ListBox, so mutating it off the UI thread races with
                // Avalonia's renderer reading the same collection and crashes the whole process
                // (IndexOutOfRangeException inside VirtualizingStackPanel), not just this dialog.
                var tcs = new TaskCompletionSource();
                RxSchedulers.MainThreadScheduler.Schedule(() =>
                {
                    tested++;
                    Results.Add(result);
                    ProgressText = string.Format(ResUI.AltIpFinderProgress, tested, total, Results.Count(r => r.TcpOk));
                    tcs.TrySetResult();
                });
                await tcs.Task;
            }, _cts.Token);

            if (_cts.Token.IsCancellationRequested)
            {
                return;
            }

            var (totalAdded, subId) = await handler.AddValidCandidatesAsGroupAsync(_source, results);
            CreatedSubId = subId;

            NoticeManager.Instance.Enqueue(string.Format(ResUI.AltIpFinderDone, Math.Max(totalAdded, 0)));
        }
        catch (Exception ex)
        {
            Logging.SaveLog("AltIpFinder", ex);
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
        }
        finally
        {
            IsRunning = false;
        }
    }

    private async Task SaveSettingsAsync()
    {
        var settings = _config.AltIpFinderItem ??= new();
        settings.CustomFofaApiKey = CustomFofaApiKey;
        settings.FofaCountryCode = CountryCode;
        settings.SampleCount = SampleCount;
        settings.TestConcurrency = TestConcurrency;
        settings.CustomQuery = Query;
        await ConfigHandler.SaveConfig(_config);
    }
}
