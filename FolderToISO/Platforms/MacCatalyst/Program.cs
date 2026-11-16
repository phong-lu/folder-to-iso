using FolderToISO.Services;
using UIKit;

namespace FolderToISO;

public static class Program
{
    public static void Main(string[] args)
    {
        // Keep the installed app's headless image creation entry point for automation.
        if (args.FirstOrDefault() == "--create")
        {
            Environment.ExitCode = IsoCommand.RunAsync(args).GetAwaiter().GetResult();
            return;
        }
        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}
