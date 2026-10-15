using Microsoft.UI.Xaml.Navigation;
using NSLocker.Services;
using NSLocker.Ui.Services;
using NSLocker.Ui.ViewModels;

namespace NSLocker.Ui.Pages;

public sealed partial class ProtocolPage : Page
{
    public ShellViewModel Shell => App.Shell;
    public ProtocolVm Vm { get; private set; } = App.Shell.Mdns;

    public ProtocolPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        Vm = (e.Parameter as string) switch
        {
            "llmnr" => Shell.Llmnr,
            "dnssec" => Shell.Dnssec,
            "ipv6" => Shell.Ipv6,
            _ => Shell.Mdns,
        };
        Bindings.Update();
    }

    public string ToggleText(bool enabled, string name) => (enabled ? "Désactiver " : "Activer ") + name;
    public Visibility DnssecVisibility(string key) => key == "dnssec" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HasText(string s) => string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility HasDnssecDiag(ViewModels.DnssecDiagnosticsVm? d) => d == null ? Visibility.Collapsed : Visibility.Visible;
    // IPv6 est lu par le pilote Tcpip6 au démarrage du système, pas par le service Dnscache :
    // redémarrer Dnscache n'aurait aucun effet sur ce réglage, contrairement à mDNS/LLMNR/DNSSEC.
    public Visibility UsesDnscacheRestart(string key) => key == "ipv6" ? Visibility.Collapsed : Visibility.Visible;

    private async void Toggle_Click(object sender, RoutedEventArgs e) => await Shell.ToggleAsync(Vm);
    private async void Restart_Click(object sender, RoutedEventArgs e) => await Shell.RestartDnscacheAsync();
    private async void DnssecDiag_Click(object sender, RoutedEventArgs e) => await Shell.RunDnssecDiagnosticsAsync();

    private async void Script_Click(object sender, RoutedEventArgs e)
    {
        bool targetEnable = !Vm.IsEnabled;
        string script = Vm.Info.Key switch
        {
            "mdns" => PowerShellScriptBuilder.Mdns(targetEnable),
            "llmnr" => PowerShellScriptBuilder.Llmnr(targetEnable),
            "dnssec" => PowerShellScriptBuilder.Dnssec(targetEnable),
            "ipv6" => PowerShellScriptBuilder.Ipv6(targetEnable),
            _ => "",
        };
        await ScriptDialogHelper.ShowAsync(XamlRoot, "Script PowerShell — " + Vm.Name, script);
    }
}
