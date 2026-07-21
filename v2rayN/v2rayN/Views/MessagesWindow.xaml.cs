using v2rayN.Common;

namespace v2rayN.Views;

public partial class MessagesWindow
{
    public MessagesWindow()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.Messages, v => v.lstMessages.ItemsSource).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.IsEmpty, v => v.txtEmpty.Visibility).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.ClearAllCmd, v => v.btnClearAll).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.CloseCmd, v => v.btnClose).DisposeWith(disposables);

            ViewModel.ShowYesNoInteraction.RegisterHandler(interaction =>
            {
                var message = interaction.Input;
                var result = UI.ShowYesNo(message) != MessageBoxResult.No;
                interaction.SetOutput(result);
            }).DisposeWith(disposables);
        });
        WindowsUtils.SetDarkBorder(this, AppManager.Instance.Config.UiItem.CurrentTheme);
    }
}
