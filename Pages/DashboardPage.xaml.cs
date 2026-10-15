using NSLocker.Ui.Services;
using NSLocker.Ui.ViewModels;

namespace NSLocker.Ui.Pages;

public sealed partial class DashboardPage : Page
{
    public ShellViewModel Shell => App.Shell;

    public DashboardPage()
    {
        InitializeComponent();
        Overview.Text = Explainers.Overview;
    }

    public string NtlmShortLabel(int level) => $"Niveau {level}";

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await Shell.RefreshAsync();

    private void Card_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is string tag) App.MainWindow?.SelectNav(tag);
    }
}
