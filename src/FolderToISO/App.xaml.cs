using Microsoft.UI.Xaml;
using FolderToISO.Services;
namespace FolderToISO;
public partial class App : Application
{
    private Window? window;
    public App() => InitializeComponent();
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var options = StartupOptions.Parse(Environment.GetCommandLineArgs()[1..]);
        window = new MainWindow(options.SourceFolder, options.Error);
        window.Activate();
    }
}
