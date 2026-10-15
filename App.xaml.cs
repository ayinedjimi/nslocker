using NSLocker.Ui.ViewModels;

namespace NSLocker.Ui;

public partial class App : Application
{
    private Window? _window;

    /// <summary>État partagé par toutes les pages (statuts, actions, notifications).</summary>
    public static ShellViewModel Shell { get; } = new();

    public static MainWindow? MainWindow { get; private set; }

    static App()
    {
        // Requis par Windows App SDK autonome : à poser avant toute résolution du Windows App Runtime.
        Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);
    }

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = MainWindow = new MainWindow();
        _window.Activate();
    }
}
