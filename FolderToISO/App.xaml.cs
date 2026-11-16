using FolderToISO.Services;

namespace FolderToISO;

public partial class App : Application
{
    public App() => InitializeComponent();

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var options = StartupOptions.Parse(Environment.GetCommandLineArgs()[1..]);
        var page = new MainPage(options.SourceFolder, options.Error);
        var window = new Window(page) { Title = "FolderToISO", Width = 800, Height = 620,
            MinimumWidth = 640, MinimumHeight = 480 };
        window.Destroying += (_, _) => page.CancelCreation();
        return window;
    }
}
