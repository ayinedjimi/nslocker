using NSLocker.Services;
using NSLocker.Ui.Services;
using NSLocker.Ui.ViewModels;

namespace NSLocker.Ui.Pages;

public sealed partial class NbtNsPage : Page
{
    public ShellViewModel Shell => App.Shell;

    public NbtNsPage()
    {
        InitializeComponent();
        var info = Explainers.NbtNs;
        Subtitle.Text = info.Subtitle;
        RiskText.Text = info.Risk;
        SideText.Text = info.SideEffects;
        Note.Text = info.ApplyNote;
        Shell.Nbt.CollectionChanged += (_, _) => Empty.Visibility = Shell.Nbt.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is ProtocolVm vm) await Shell.ToggleAsync(vm);
    }

    private async void Script_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not ProtocolVm vm) return;
        string script = PowerShellScriptBuilder.NbtNs(vm.Id, vm.Name, !vm.IsEnabled);
        await ScriptDialogHelper.ShowAsync(XamlRoot, "Script PowerShell — NBT-NS : " + vm.Name, script);
    }
}
