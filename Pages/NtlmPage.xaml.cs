using Microsoft.UI.Xaml.Media;
using NSLocker.Services;
using NSLocker.Ui.Services;
using NSLocker.Ui.ViewModels;

namespace NSLocker.Ui.Pages;

public sealed partial class NtlmPage : Page
{
    public ShellViewModel Shell => App.Shell;
    public string NtlmRisk => Explainers.NtlmRisk;
    public string NtlmSideEffects => Explainers.NtlmSideEffects;
    public string NtlmApplyNote => Explainers.NtlmApplyNote;

    public NtlmPage() => InitializeComponent();

    public string CurrentIcon(bool hardened) => hardened ? "" : ""; // coche / avertissement
    public Brush CurrentBrush(bool hardened) =>
        (Brush)Application.Current.Resources[hardened ? "StateOkBrush" : "StateCritBrush"];

    private async void Apply_Click(object sender, RoutedEventArgs e) => await Shell.ApplyNtlmLevelAsync();
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await Shell.RefreshAsync();

    private async void Script_Click(object sender, RoutedEventArgs e)
    {
        string script = PowerShellScriptBuilder.Ntlm((int)Shell.PendingNtlmLevel);
        await ScriptDialogHelper.ShowAsync(XamlRoot, "Script PowerShell — NTLM", script);
    }
}
