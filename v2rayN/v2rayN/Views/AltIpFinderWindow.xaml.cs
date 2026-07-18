namespace v2rayN.Views;

public partial class AltIpFinderWindow
{
    public AltIpFinderWindow()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.CustomFofaApiKey, v => v.txtFofaApiKey.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.Presets, v => v.cmbPreset.ItemsSource).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedPreset, v => v.cmbPreset.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Query, v => v.txtQuery.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SampleCount, v => v.txtSampleCount.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.TestConcurrency, v => v.txtConcurrency.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.ProgressText, v => v.txtProgress.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.Results, v => v.lstResults.ItemsSource).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.StartCmd, v => v.btnStart).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.CancelCmd, v => v.btnCancel).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.CloseCmd, v => v.btnClose).DisposeWith(disposables);
        });
        WindowsUtils.SetDarkBorder(this, AppManager.Instance.Config.UiItem.CurrentTheme);
    }
}
