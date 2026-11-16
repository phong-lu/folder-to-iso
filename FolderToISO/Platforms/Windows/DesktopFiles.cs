using Windows.Storage.Pickers;
using WinRT.Interop;

namespace FolderToISO;

internal static partial class DesktopFiles
{
    private static nint Handle(Window window) => WindowNative.GetWindowHandle(window.Handler!.PlatformView);

    internal static partial async Task<string?> PickSourceAsync(Window window)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, Handle(window));
        return (await picker.PickSingleFolderAsync())?.Path;
    }

    internal static partial async Task<string?> PickOutputAsync(Window window, string suggestedPath)
    {
        var picker = new FileSavePicker { SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedPath) };
        picker.FileTypeChoices.Add("Disc image", new List<string> { ".iso" });
        InitializeWithWindow.Initialize(picker, Handle(window));
        return (await picker.PickSaveFileAsync())?.Path;
    }

    internal static partial async Task OpenFolderAsync(string folder)
    {
        if (!await Windows.System.Launcher.LaunchFolderPathAsync(folder))
            throw new IOException($"Could not open folder: {folder}");
    }
}
