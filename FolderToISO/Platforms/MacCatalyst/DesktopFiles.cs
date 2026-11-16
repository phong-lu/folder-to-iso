using Foundation;
using UIKit;
using UniformTypeIdentifiers;

namespace FolderToISO;

internal static partial class DesktopFiles
{
    internal static partial Task<string?> PickSourceAsync(Window window) => PickFolderAsync(window);

    internal static partial async Task<string?> PickOutputAsync(Window window, string suggestedPath)
    {
        // Select the destination directory without creating a placeholder ISO.
        // The filename remains editable in the shared ISO file entry.
        var folder = await PickFolderAsync(window);
        return folder == null ? null : Path.Combine(folder,
            string.IsNullOrWhiteSpace(suggestedPath) ? "Image.iso" : Path.GetFileName(suggestedPath));
    }

    private static async Task<string?> PickFolderAsync(Window window)
    {
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var picker = new UIDocumentPickerViewController([UTTypes.Folder], false);
        using var handler = new FolderDelegate(completion);
        picker.Delegate = handler;
        picker.AllowsMultipleSelection = false;
        var native = (UIWindow)window.Handler!.PlatformView!;
        var presenter = native.RootViewController!;
        while (presenter.PresentedViewController != null) presenter = presenter.PresentedViewController;
        presenter.PresentViewController(picker, true, null);
        return await completion.Task;
    }

    private sealed class FolderDelegate(TaskCompletionSource<string?> completion) : UIDocumentPickerDelegate
    {
        public override void DidPickDocument(UIDocumentPickerViewController controller, NSUrl[] urls) =>
            completion.TrySetResult(urls.FirstOrDefault()?.Path);
        public override void WasCancelled(UIDocumentPickerViewController controller) => completion.TrySetResult(null);
    }

    internal static partial async Task OpenFolderAsync(string folder)
    {
        using var url = NSUrl.FromFilename(folder);
        if (!await UIApplication.SharedApplication.OpenUrlAsync(url, new UIApplicationOpenUrlOptions()))
            throw new IOException($"Could not open folder: {folder}");
    }
}
