namespace FolderToISO;

internal static partial class DesktopFiles
{
    internal static partial Task<string?> PickSourceAsync(Window window);
    internal static partial Task<string?> PickOutputAsync(Window window, string suggestedPath);
    internal static partial Task OpenFolderAsync(string folder);
}
