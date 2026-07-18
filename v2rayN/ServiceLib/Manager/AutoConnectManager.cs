namespace ServiceLib.Manager;

/// <summary>
/// Periodically speed-tests the servers in the active profile's subscription group and
/// switches the active connection to whichever one currently tests best (highest speed,
/// then lowest delay). Driven by TaskManager's 1-minute tick; only actually runs a check
/// once AutoConnectItem.IntervalMinutes has elapsed since the last one, and only for a
/// subscription group that has explicitly opted in via SubItem.AutoConnectEnabled - this is
/// a per-group setting, never a blanket toggle across every subscription.
/// </summary>
public class AutoConnectManager
{
    private static readonly Lazy<AutoConnectManager> _instance = new(() => new());
    public static AutoConnectManager Instance => _instance.Value;
    private static readonly string _tag = "AutoConnectManager";
    private bool _isRunning;

    public async Task CheckAndSwitchAsync(Config config)
    {
        if (_isRunning)
        {
            return;
        }

        var settings = config.AutoConnectItem ??= new();

        var now = ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds();
        if (now - settings.LastCheckTime < settings.IntervalMinutes * 60)
        {
            return;
        }

        _isRunning = true;
        try
        {
            await RunCheckAsync(config, settings);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        finally
        {
            settings.LastCheckTime = now;
            await ConfigHandler.SaveConfig(config);
            _isRunning = false;
        }
    }

    private async Task RunCheckAsync(Config config, AutoConnectItem settings)
    {
        var currentId = config.IndexId;
        if (currentId.IsNullOrEmpty())
        {
            return;
        }

        var current = await AppManager.Instance.GetProfileItem(currentId);
        if (current is null || current.Subid.IsNullOrEmpty())
        {
            return;
        }

        var subItem = await AppManager.Instance.GetSubItem(current.Subid);
        if (subItem is not { AutoConnectEnabled: true })
        {
            return;
        }

        var groupProfiles = (await AppManager.Instance.ProfileItems(current.Subid))?
            .Where(p => p.ConfigType != EConfigType.Custom && (p.ConfigType.IsComplexType() || p.Port > 0))
            .Take(Math.Max(settings.TestBatchSize, 1))
            .ToList();

        if (groupProfiles is not { Count: > 0 })
        {
            return;
        }

        Logging.SaveLog($"{_tag}: testing {groupProfiles.Count} server(s) in group for a possibly better connection");

        var tcs = new TaskCompletionSource();
        var speedtestService = new SpeedtestService(config, async result =>
        {
            if (result.IndexId.IsNullOrEmpty() && result.Delay == ResUI.SpeedtestingCompleted)
            {
                tcs.TrySetResult();
            }
            await Task.CompletedTask;
        });
        speedtestService.RunLoop(ESpeedActionType.Mixedtest, groupProfiles);

        var finished = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromMinutes(5)));
        if (finished != tcs.Task)
        {
            return;
        }

        var groupIds = groupProfiles.Select(p => p.IndexId).ToHashSet();
        var exs = await ProfileExManager.Instance.GetProfileExs();
        var best = exs
            .Where(e => groupIds.Contains(e.IndexId) && e.Delay > 0)
            .OrderByDescending(e => e.Speed)
            .ThenBy(e => e.Delay)
            .FirstOrDefault();

        if (best is null || best.IndexId == currentId)
        {
            return;
        }

        if (await ConfigHandler.SetDefaultServerIndex(config, best.IndexId) == 0)
        {
            Logging.SaveLog($"{_tag}: switched to a better server {best.IndexId} (delay={best.Delay}ms, speed={best.Speed})");
            AppEvents.CoreReloadRequested.Publish();
        }
    }
}
