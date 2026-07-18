using System.Reactive.Concurrency;
using System.Runtime.InteropServices;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;

namespace v2rayN.Desktop.Common;

/// <summary>
/// Shows a real Windows balloon/toast (Shell_NotifyIcon NIF_INFO) directly via P/Invoke.
/// Avalonia's own TrayIcon has no notification API, and the modern WinRT toast API normally
/// needs a Start Menu shortcut with a registered AUMID to work reliably for an unpackaged app -
/// the legacy Shell_NotifyIcon balloon has neither requirement and Windows 10/11 still promotes
/// it to a normal Action Center toast visually.
/// </summary>
internal static class Win32ToastNotifier
{
    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_INFO = 0x00000010;
    private const uint NIIF_INFO = 0x00000001;
    private static readonly IntPtr IDI_INFORMATION = 32516;
    private const uint WM_APP_TRAY = 0x8001;
    private const uint IconId = 9199;
    private static readonly TimeSpan _lingerDuration = TimeSpan.FromSeconds(15);

    private static bool _added;
    private static CancellationTokenSource? _removeCts;

    public static void Init()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        AppEvents.ToastNotificationRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(payload => Show(payload));
    }

    private static void Show(ToastNotificationPayload payload)
    {
        try
        {
            var hWnd = GetMainWindowHandle();
            if (hWnd == IntPtr.Zero)
            {
                Logging.SaveLog("Win32ToastNotifier: no valid window handle, cannot show notification");
                return;
            }

            var message = payload.ImageUrl.IsNotEmpty()
                ? $"{payload.Message}\n(برای مشاهده‌ی کامل به کانال مراجعه کنید)"
                : payload.Message;

            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = hWnd,
                uID = IconId,
                uFlags = NIF_INFO | NIF_ICON | NIF_MESSAGE,
                uCallbackMessage = WM_APP_TRAY,
                hIcon = NativeMethods.LoadIcon(IntPtr.Zero, IDI_INFORMATION),
                szTip = payload.Title,
                szInfo = message,
                szInfoTitle = payload.Title,
                dwInfoFlags = NIIF_INFO,
            };

            // NIM_MODIFY only succeeds if this exact (hWnd, uID) pair is still registered; if the
            // native window was ever recreated (e.g. by a hide/show-to-tray cycle) that pairing
            // goes stale and MODIFY silently fails - explicitly fall back to NIM_ADD so a single
            // missed registration doesn't permanently break every notification afterwards.
            var ok = _added && NativeMethods.Shell_NotifyIcon(NIM_MODIFY, ref data);
            if (!ok)
            {
                ok = NativeMethods.Shell_NotifyIcon(NIM_ADD, ref data);
            }
            _added = ok;
            Logging.SaveLog($"Win32ToastNotifier: Shell_NotifyIcon result = {ok}");

            // This icon only exists to carry a balloon; it must not linger in the tray as
            // permanent clutter once the balloon has had time to display, so remove it again
            // after a short delay. A fresh notification just re-adds it.
            if (ok)
            {
                ScheduleRemoval(hWnd);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("Win32ToastNotifier", ex);
        }
    }

    private static void ScheduleRemoval(IntPtr hWnd)
    {
        _removeCts?.Cancel();
        var cts = new CancellationTokenSource();
        _removeCts = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_lingerDuration, cts.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            RxSchedulers.MainThreadScheduler.Schedule(() =>
            {
                if (cts.IsCancellationRequested)
                {
                    return;
                }
                var data = new NOTIFYICONDATA
                {
                    cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                    hWnd = hWnd,
                    uID = IconId,
                };
                NativeMethods.Shell_NotifyIcon(NIM_DELETE, ref data);
                _added = false;
            });
        });
    }

    private static IntPtr GetMainWindowHandle()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return IntPtr.Zero;
        }
        var window = desktop.MainWindow;
        var handle = window?.TryGetPlatformHandle();
        return handle?.Handle ?? IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
    }

    private static class NativeMethods
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll")]
        public static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);
    }
}
