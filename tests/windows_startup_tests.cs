using System.Diagnostics;
using System.IO;
using System.Windows.Automation;

if (args.Length != 1) throw new ArgumentException("Pass the published FolderToISO.exe path.");
var executable = Path.GetFullPath(args[0]);
var source = Path.Combine(Path.GetTempPath(), "FolderToISO startup " + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(source);
try
{
    Check(null, false);
    Check(source, true);
    Check(Path.Combine(source, "missing"), false);
}
finally { Directory.Delete(source); }

void Check(string? folder, bool enabled)
{
    var start = new ProcessStartInfo(executable) { WorkingDirectory = Path.GetTempPath(), WindowStyle = ProcessWindowStyle.Hidden };
    if (folder != null) { start.ArgumentList.Add("--source"); start.ArgumentList.Add(folder); }
    using var app = Process.Start(start) ?? throw new Exception("Could not launch the application.");
    try
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        AutomationElement? button = null;
        while (DateTime.UtcNow < deadline)
        {
            app.Refresh();
            if (app.HasExited) throw new Exception($"Application exited: {app.ExitCode}");
            if (app.MainWindowHandle != IntPtr.Zero)
            {
                var window = AutomationElement.FromHandle(app.MainWindowHandle);
                button = window.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.NameProperty, "Create ISO"));
                if (button != null) break;
            }
            Thread.Sleep(100);
        }
        if (button == null) throw new Exception("Create ISO button was not exposed.");
        Thread.Sleep(500);
        if (button.Current.IsEnabled != enabled)
            throw new Exception($"Create ISO enabled={button.Current.IsEnabled}, expected {enabled}; source={folder ?? "(empty)"}");
        Console.WriteLine($"PASS: Create ISO enabled={enabled}; source={folder ?? "(empty)"}");
    }
    finally { if (!app.HasExited) app.Kill(); app.WaitForExit(); }
}
