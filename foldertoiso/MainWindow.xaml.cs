using FolderToISO.Services;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using Windows.Storage.Pickers;
using WinRT.Interop;
namespace FolderToISO;
public sealed partial class MainWindow : Window
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr window, out NativeRect rect);

    private CancellationTokenSource? cancellation;
    private string? createdIsoPath;
    private string? suggestedSource;
    public MainWindow(string? sourceFolder = null, string? startupError = null)
    {
        InitializeComponent();
        // The WinUI control manages caption spacing, drag regions, and theme/activation states.
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        SourceBox.TextChanged += SourceBox_TextChanged;
        OutputBox.TextChanged += (_, _) => UpdateDestinationState();
        Title = "FolderToISO";
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "FolderToISO.ico"));
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
        }
        Closed += (_, _) => cancellation?.Cancel();
        if (sourceFolder != null)
        {
            SourceBox.Text = sourceFolder;
            UpdateSuggestedDestination();
            if (!Directory.Exists(sourceFolder))
                startupError = "The selected source folder no longer exists. Choose another folder.";
        }
        if (startupError != null) StatusText.Text = startupError;
    }
    private void LayoutPanel_Loaded(object sender, RoutedEventArgs e)
    {
        // Measure in XAML units, then size the client area in physical pixels for this display.
        var scale = LayoutPanel.XamlRoot.RasterizationScale;
        var workArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
            AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Min(800, (workArea.Width - 32) / scale);
        LayoutPanel.Measure(new Windows.Foundation.Size(width, double.PositiveInfinity));
        AppTitleBar.Measure(new Windows.Foundation.Size(width, double.PositiveInfinity));
        // Use the actual client/frame difference: ResizeClient's title-bar adjustment does
        // not match the XAML viewport when content extends into the title bar.
        if (!GetClientRect(WindowNative.GetWindowHandle(this), out var client)) return;
        var frameWidth = AppWindow.Size.Width - (client.Right - client.Left);
        var frameHeight = AppWindow.Size.Height - (client.Bottom - client.Top);
        var height = LayoutPanel.DesiredSize.Height + AppTitleBar.DesiredSize.Height;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(
            (int)Math.Ceiling(width * scale) + frameWidth,
            Math.Min((int)Math.Ceiling(height * scale) + frameHeight, workArea.Height - 32)));
    }

    private void SourceBox_TextChanged(object sender, Microsoft.UI.Xaml.Controls.TextChangedEventArgs e)
    {
        var invalidVisibility = Directory.Exists(SourceBox.Text.Trim()) ? Visibility.Collapsed : Visibility.Visible;
        InvalidSourceText.Visibility = invalidVisibility;
        UpdateSuggestedDestination();
        UpdateDestinationState();
        if (LayoutPanel.XamlRoot != null)
            LayoutPanel_Loaded(this, new RoutedEventArgs());
    }
    private void UpdateDestinationState()
    {
        var fat32 = false;
        try
        {
            var destination = OutputBox.Text.Trim();
            if (Path.IsPathFullyQualified(destination))
            {
                var drive = new DriveInfo(Path.GetPathRoot(destination)!);
                fat32 = drive.IsReady && drive.DriveFormat.Equals("FAT32", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Incomplete or unavailable destinations are validated when creating the ISO.
        }
        var wasOpen = SummaryFat32Warning.Visibility == Visibility.Visible;
        SummaryFat32Warning.Visibility = EditorFat32Warning.Visibility = fat32 ? Visibility.Visible : Visibility.Collapsed;
        var validSource = Directory.Exists(SourceBox.Text.Trim());
        ChangeDestinationButton.Visibility = validSource ? Visibility.Visible : Visibility.Collapsed;
        CreateButton.IsEnabled = cancellation == null && !fat32 && validSource;
        if (wasOpen != fat32 && LayoutPanel.XamlRoot != null)
            LayoutPanel_Loaded(this, new RoutedEventArgs());
    }
    private void SourceBox_LostFocus(object sender, RoutedEventArgs e) => UpdateSuggestedDestination();

    private void UpdateSuggestedDestination()
    {
        var source = SourceBox.Text.Trim();
        if (source == suggestedSource) return;
        suggestedSource = source;
        OutputBox.Text = IsoBuilder.SuggestedOutputPath(SourceBox.Text);
        var folderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(source));
        LabelBox.Text = string.IsNullOrWhiteSpace(folderName) ? string.Empty : folderName[..Math.Min(folderName.Length, LabelBox.MaxLength)];
        OutputSummary.Text = string.IsNullOrEmpty(source) ? string.Empty : $"ISO file: {OutputBox.Text}";
        LabelSummary.Text = string.IsNullOrEmpty(source) ? string.Empty : $"Volume label: {LabelBox.Text}";
        if (string.IsNullOrEmpty(source))
        {
            DestinationSummary.Visibility = OutputEditor.Visibility = LabelEditor.Visibility = Visibility.Collapsed;
        }
        else if (OutputEditor.Visibility != Visibility.Visible)
        {
            DestinationSummary.Visibility = Visibility.Visible;
        }
    }
    private void ChangeDestination_Click(object sender, RoutedEventArgs e)
    {
        UpdateSuggestedDestination();
        DestinationSummary.Visibility = Visibility.Collapsed;
        OutputEditor.Visibility = LabelEditor.Visibility = Visibility.Visible;
        LayoutPanel_Loaded(sender, e);
        OutputBox.Focus(FocusState.Programmatic);
    }
    private async void BrowseSource_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                SourceBox.Text = folder.Path;
                UpdateSuggestedDestination();
            }
        }
        catch (Exception ex) { StatusText.Text = ex.Message; }
    }
    private async void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileSavePicker { SuggestedFileName = "Image" };
            picker.FileTypeChoices.Add("Disc image", new List<string> { ".iso" });
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            var file = await picker.PickSaveFileAsync();
            if (file != null) OutputBox.Text = file.Path;
        }
        catch (Exception ex) { StatusText.Text = ex.Message; }
    }
    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        if (cancellation != null) return;
        UpdateSuggestedDestination();
        UpdateDestinationState();
        if (!CreateButton.IsEnabled) return;
        if (File.Exists(OutputBox.Text))
        {
            var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                XamlRoot = Content.XamlRoot, Title = "Overwrite existing ISO?",
                Content = $"The following file already exists:\n\n{OutputBox.Text}\n\nDo you want to overwrite it?",
                PrimaryButtonText = "Overwrite", CloseButtonText = "Cancel",
                DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() != Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary) return;
        }
        createdIsoPath = null;
        OpenFolderButton.Visibility = Visibility.Collapsed;
        cancellation = new CancellationTokenSource();
        SetBusy(true);
        try
        {
            var progress = new Progress<IsoProgress>(p =>
            {
                ProgressBar.IsIndeterminate = p.Percent == null;
                if (p.Percent is double percent) ProgressBar.Value = percent;
                StatusText.Text = p.Message;
                if (p.Speed != null)
                {
                    StatusText.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
                    {
                        Text = $" ({p.Speed})",
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Windows.UI.Color.FromArgb(255, 171, 223, 167))
                    });
                }
            });
            await IsoBuilder.CreateAsync(SourceBox.Text, OutputBox.Text, LabelBox.Text, progress, cancellation.Token);
            createdIsoPath = Path.GetFullPath(OutputBox.Text);
            StatusText.Text = $"Created {createdIsoPath}";
            OpenFolderButton.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException) { StatusText.Text = "Cancelled. No incomplete ISO was kept."; }
        catch (Exception ex) { StatusText.Text = $"Could not create ISO: {ex.Message}"; }
        finally
        {
            cancellation.Dispose(); cancellation = null;
            ProgressBar.IsIndeterminate = false;
            SetBusy(false);
        }
    }
    private async void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (createdIsoPath == null) return;
        try
        {
            var folder = Path.GetDirectoryName(createdIsoPath)!;
            if (!await Windows.System.Launcher.LaunchFolderPathAsync(folder))
                StatusText.Text = $"Could not open folder: {folder}";
        }
        catch (Exception ex) { StatusText.Text = $"Could not open folder: {ex.Message}"; }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        cancellation?.Cancel();
        CancelButton.IsEnabled = false;
        StatusText.Text = "Cancelling... Image preparation may need to finish first.";
    }
    private void SetBusy(bool busy)
    {
        SourceBox.IsEnabled = OutputBox.IsEnabled = LabelBox.IsEnabled = !busy;
        LabelDescription.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[
            busy ? "TextControlHeaderForegroundDisabled" : "TextControlHeaderForeground"];
        SourceButton.IsEnabled = OutputButton.IsEnabled = !busy;
        ChangeDestinationButton.IsEnabled = !busy;
        UpdateDestinationState();
        CancelButton.IsEnabled = busy;
        ProgressBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        if (busy) ProgressBar.Value = 0;
        // Refit after showing/hiding progress so the bottom inset stays at the panel's 32px padding.
        if (LayoutPanel.XamlRoot != null)
            LayoutPanel_Loaded(this, new RoutedEventArgs());
    }
}





