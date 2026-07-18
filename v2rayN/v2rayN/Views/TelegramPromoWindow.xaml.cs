using System.Windows;
using ServiceLib.Common;

namespace v2rayN.Views;

public partial class TelegramPromoWindow : Window
{
    public TelegramPromoWindow()
    {
        InitializeComponent();

        btnChannel.Click += (s, e) => ProcUtils.ProcessStart(Global.TelegramChannelUrl);
        btnAdmin.Click += (s, e) => ProcUtils.ProcessStart(Global.TelegramAdminUrl);
        btnClose.Click += (s, e) => Close();
    }
}
