using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml.Media;
using NSLocker.Ui.Pages;
using NSLocker.Ui.ViewModels;

namespace NSLocker.Ui;

public sealed partial class MainWindow : Window
{
    public ShellViewModel Shell => App.Shell;

    public MainWindow()
    {
        InitializeComponent();
        Title = "NSLOCKER";
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        try { AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico")); }
        catch { /* icône absente : la fenêtre s'ouvre quand même */ }
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1100, 760));

        Nav.SelectedItem = Nav.MenuItems[0];
        Navigate("dashboard");
        _ = Shell.RefreshAsync();
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is string tag) Navigate(tag);
    }

    /// <summary>Navigation par clé : dashboard, mdns, llmnr, dnssec, ipv6, nbtns, ntlm.</summary>
    public void Navigate(string tag)
    {
        var page = tag switch { "dashboard" => typeof(DashboardPage), "nbtns" => typeof(NbtNsPage), "ntlm" => typeof(NtlmPage), _ => typeof(ProtocolPage) };
        ContentFrame.Navigate(page, tag, new Microsoft.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo());
    }

    public void SelectNav(string tag)
    {
        foreach (var item in Nav.MenuItems.OfType<NavigationViewItem>())
            if ((string?)item.Tag == tag) { Nav.SelectedItem = item; return; }
    }

    private void InfoBar_Closed(InfoBar sender, InfoBarClosedEventArgs args) => Shell.Dismiss();
}
