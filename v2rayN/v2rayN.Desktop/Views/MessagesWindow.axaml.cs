using v2rayN.Desktop.Base;
using v2rayN.Desktop.Common;

namespace v2rayN.Desktop.Views;

public partial class MessagesWindow : WindowBase<MessagesViewModel>
{
    public MessagesWindow()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.Messages, v => v.lstMessages.ItemsSource).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.IsEmpty, v => v.txtEmpty.IsVisible).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.ClearAllCmd, v => v.btnClearAll).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.CloseCmd, v => v.btnClose).DisposeWith(disposables);

            ViewModel!.ShowYesNoInteraction.RegisterHandler(async interaction =>
            {
                var result = await UI.ShowYesNo(interaction.Input);
                interaction.SetOutput(result == ButtonResult.Yes);
            }).DisposeWith(disposables);
        });
    }
}
