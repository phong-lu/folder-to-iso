using FolderToISO.Services;

namespace FolderToISO.Linux;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (!OperatingSystem.IsLinux())
        {
            Console.Error.WriteLine("This build requires Linux. Build DesktopPlatform=Windows for Windows.");
            return 1;
        }
        try
        {
            if (Gtk.gtk_init_check(IntPtr.Zero, IntPtr.Zero) == 0)
                throw new InvalidOperationException("A graphical desktop session is required (DISPLAY or WAYLAND_DISPLAY).");
            var options = StartupOptions.Parse(args);
            var window = new LinuxWindow(options.SourceFolder, options.Error);
            window.Run();
            GC.KeepAlive(window);
            return 0;
        }
        catch (DllNotFoundException)
        {
            Console.Error.WriteLine("GTK 3 is required. Install libgtk-3-0 (Ubuntu/Debian) or gtk3 (Fedora). ");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
