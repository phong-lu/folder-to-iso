using FolderToISO.Services;

namespace FolderToISO;

public partial class MainPage : ContentPage
{
    private CancellationTokenSource? cancellation;
    private string? createdPath;
    private bool ready;

    public MainPage(string? sourceFolder = null, string? startupError = null)
    {
        InitializeComponent();
        ready = true;
#if MACCATALYST
        OutputButton.Text = "Choose folder…";
#endif
        if (sourceFolder != null) SourceBox.Text = sourceFolder;
        if (startupError != null) Status.Text = startupError;
        Loaded += (_, _) => UpdateState();
        UpdateState();
    }

    private void SourceChanged(object? sender, TextChangedEventArgs e)
    {
        if (!ready) return;
        var source = SourceBox.Text?.Trim() ?? "";
        OutputBox.Text = IsoBuilder.SuggestedOutputPath(source);
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(source));
        LabelBox.Text = string.IsNullOrEmpty(name) ? "FolderToISO" : name[..Math.Min(name.Length, 32)];
        UpdateState();
    }

    private void DestinationChanged(object? sender, TextChangedEventArgs e)
    {
        if (ready) UpdateState();
    }

    private void UpdateState()
    {
        var valid = Directory.Exists(SourceBox.Text?.Trim());
        InvalidSource.IsVisible = !string.IsNullOrWhiteSpace(SourceBox.Text) && !valid;
        CreateButton.IsEnabled = cancellation == null && valid;
        Fat32Warning.IsVisible = false;
        try
        {
            if (Path.IsPathFullyQualified(OutputBox.Text ?? ""))
            {
                var drive = new DriveInfo(Path.GetDirectoryName(OutputBox.Text)!);
                Fat32Warning.IsVisible = drive.IsReady && drive.DriveFormat.Equals("FAT32", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
    }

    private async void BrowseSource(object? sender, EventArgs e)
    {
        try { if (await DesktopFiles.PickSourceAsync(Window) is { } path) SourceBox.Text = path; }
        catch (Exception ex) { Status.Text = ex.Message; }
    }

    private async void BrowseOutput(object? sender, EventArgs e)
    {
        try { if (await DesktopFiles.PickOutputAsync(Window, OutputBox.Text ?? "Image.iso") is { } path) OutputBox.Text = path; }
        catch (Exception ex) { Status.Text = ex.Message; }
    }

    private async void CreateIso(object? sender, EventArgs e)
    {
        if (cancellation != null) return;
        // Capture the request before awaiting replacement confirmation.
        var source = SourceBox.Text?.Trim() ?? "";
        var output = OutputBox.Text?.Trim() ?? "";
        var label = LabelBox.Text ?? "";
        cancellation = new CancellationTokenSource();
        Editors.IsEnabled = CreateButton.IsEnabled = false;
        OpenButton.IsVisible = false;
        createdPath = null;
        try
        {
            var replace = File.Exists(output);
            if (replace && !await DisplayAlertAsync("Overwrite existing ISO?", output, "Overwrite", "Cancel")) return;
            CancelButton.IsEnabled = true;
            Activity.IsVisible = Activity.IsRunning = true;
            Progress.Progress = 0;
            var progress = new Progress<IsoProgress>(p =>
            {
                Activity.IsVisible = Activity.IsRunning = p.Percent == null;
                Progress.IsVisible = p.Percent != null;
                if (p.Percent is double percent) Progress.Progress = Math.Clamp(percent / 100, 0, 1);
                Status.Text = p.Speed == null ? p.Message : $"{p.Message} ({p.Speed})";
            });
            await IsoBuilder.CreateAsync(source, output, label, progress, cancellation.Token, overwrite: replace);
            createdPath = Path.GetFullPath(output);
            Status.Text = $"Created {createdPath}";
            OpenButton.IsVisible = true;
        }
        catch (OperationCanceledException) { Status.Text = "Cancelled. No incomplete ISO was kept."; }
        catch (Exception ex) { Status.Text = $"Could not create ISO: {ex.Message}"; }
        finally
        {
            cancellation.Dispose();
            cancellation = null;
            Editors.IsEnabled = true;
            CancelButton.IsEnabled = Activity.IsVisible = Activity.IsRunning = Progress.IsVisible = false;
            UpdateState();
        }
    }

    public void CancelCreation() => cancellation?.Cancel();

    private void CancelIso(object? sender, EventArgs e)
    {
        CancelCreation();
        CancelButton.IsEnabled = false;
        Status.Text = "Cancelling… Image preparation may need to finish first.";
    }

    private async void OpenFolder(object? sender, EventArgs e)
    {
        if (createdPath == null) return;
        try { await DesktopFiles.OpenFolderAsync(Path.GetDirectoryName(createdPath)!); }
        catch (Exception ex) { Status.Text = ex.Message; }
    }
}
