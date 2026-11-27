using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using FolderToISO.Services;

namespace FolderToISO.Linux;

internal sealed class LinuxWindow
{
    private readonly IntPtr window, source, output, label, sourceButton, outputButton, createButton, cancelButton, status, progress;
    // Native signal handlers must stay rooted until the window and GTK main loop are gone.
    private readonly List<Delegate> callbacks = [];
    private readonly ConcurrentQueue<IsoProgress> updates = new();
    private CancellationTokenSource? cancellation;
    private Task? operation;
    private bool closing;
    private bool destroyed;
    private bool indeterminate;

    internal LinuxWindow(string? sourceFolder = null, string? startupError = null)
    {
        window = Gtk.gtk_window_new(0);
        Gtk.gtk_window_set_title(window, "FolderToISO");
        Gtk.gtk_window_set_default_size(window, 820, 560);
        Gtk.gtk_container_set_border_width(window, 24);
        var layout = Gtk.gtk_box_new(1, 14);
        Gtk.gtk_container_add(window, layout);
        AddLabel(layout, "FolderToISO — Turn a folder into a disc image");
        AddLabel(layout, "Create a UDF image, including files larger than 4 GiB. The folder's contents become the image root.");
        source = AddPath(layout, "Source folder", "Browse...", out sourceButton);
        output = AddPath(layout, "ISO file", "Save as...", out outputButton);
        AddLabel(layout, "Volume label");
        label = Gtk.gtk_entry_new();
        Gtk.gtk_entry_set_text(label, "FolderToISO");
        Gtk.gtk_entry_set_max_length(label, 32);
        Pack(layout, label);
        AddLabel(layout, "Save large images to ext4, Btrfs, XFS, NTFS or exFAT. FAT32 cannot store files larger than 4 GiB.");
        var buttons = Gtk.gtk_box_new(0, 12);
        createButton = Gtk.gtk_button_new_with_label("Create ISO");
        cancelButton = Gtk.gtk_button_new_with_label("Cancel");
        Gtk.gtk_widget_set_sensitive(cancelButton, 0);
        Pack(buttons, createButton);
        Pack(buttons, cancelButton);
        Pack(layout, buttons);
        progress = Gtk.gtk_progress_bar_new();
        Pack(layout, progress);
        status = AddLabel(layout, "Ready");
        Connect(source, "changed", () => Gtk.gtk_entry_set_text(output, IsoBuilder.SuggestedOutputPath(Text(source))));
        Connect(sourceButton, "clicked", () => Browse(source, 2));
        Connect(outputButton, "clicked", () => Browse(output, 1));
        Connect(createButton, "clicked", Create);
        Connect(cancelButton, "clicked", Cancel);
        Connect(window, "destroy", () => { destroyed = true; Gtk.gtk_main_quit(); });
        Gtk.DeleteCallback delete = (_, _, _) =>
        {
            if (operation == null) return 0;
            closing = true;
            Cancel();
            return 1; // Finish cancellation and temporary-file cleanup before closing.
        };
        callbacks.Add(delete);
        Gtk.g_signal_connect_data(window, "delete-event", Marshal.GetFunctionPointerForDelegate(delete), IntPtr.Zero, IntPtr.Zero, 0);
        Gtk.TimerCallback tick = _ => Tick();
        callbacks.Add(tick);
        Gtk.g_timeout_add(100, tick, IntPtr.Zero);
        if (sourceFolder != null)
        {
            Gtk.gtk_entry_set_text(source, sourceFolder);
            if (!Directory.Exists(sourceFolder))
                startupError = "The selected source folder no longer exists. Choose another folder.";
        }
        if (startupError != null) Gtk.gtk_label_set_text(status, startupError);
    }

    internal void Run()
    {
        Gtk.gtk_widget_show_all(window);
        Gtk.gtk_main();
    }

    private void Create()
    {
        if (operation != null) return;
        var destination = Text(output);
        if (File.Exists(destination) && !ConfirmReplace(destination)) return;
        cancellation = new CancellationTokenSource();
        SetBusy(true);
        indeterminate = true;
        Gtk.gtk_progress_bar_set_fraction(progress, 0);
        operation = IsoBuilder.CreateAsync(Text(source), destination, Text(label), new QueuedProgress(updates), cancellation.Token);
    }

    private void Cancel()
    {
        cancellation?.Cancel();
        Gtk.gtk_widget_set_sensitive(cancelButton, 0);
        Gtk.gtk_label_set_text(status, "Cancelling...");
    }

    private int Tick()
    {
        if (destroyed) return 0;
        while (updates.TryDequeue(out var update))
        {
            Gtk.gtk_label_set_text(status, update.Message);
            indeterminate = update.Percent == null;
            if (update.Percent is double value) Gtk.gtk_progress_bar_set_fraction(progress, value / 100);
        }
        if (indeterminate && operation is { IsCompleted: false }) Gtk.gtk_progress_bar_pulse(progress);
        if (operation is { IsCompleted: true })
        {
            try
            {
                operation.GetAwaiter().GetResult();
                Gtk.gtk_progress_bar_set_fraction(progress, 1);
                Gtk.gtk_label_set_text(status, $"Created {Text(output)}");
            }
            catch (OperationCanceledException) { Gtk.gtk_label_set_text(status, "Cancelled. No incomplete ISO was kept."); }
            catch (Exception ex) { Gtk.gtk_label_set_text(status, $"Could not create ISO: {ex.Message}"); }
            finally
            {
                operation = null;
                cancellation!.Dispose();
                cancellation = null;
                SetBusy(false);
            }
            if (closing) Gtk.gtk_widget_destroy(window);
        }
        return destroyed ? 0 : 1;
    }

    private void SetBusy(bool busy)
    {
        foreach (var widget in new[] { source, output, label, sourceButton, outputButton, createButton })
            Gtk.gtk_widget_set_sensitive(widget, busy ? 0 : 1);
        Gtk.gtk_widget_set_sensitive(cancelButton, busy ? 1 : 0);
    }

    private void Browse(IntPtr entry, int action)
    {
        var dialog = Gtk.gtk_file_chooser_native_new(action == 2 ? "Choose source folder" : "Save ISO", window,
            action, action == 2 ? "Select" : "Save", "Cancel");
        try
        {
            if (action == 1)
            {
                Gtk.gtk_file_chooser_set_current_name(dialog, Path.GetFileName(Text(output)) is { Length: > 0 } name ? name : "Image.iso");
                var directory = Path.GetDirectoryName(Text(output));
                if (Directory.Exists(directory)) Gtk.gtk_file_chooser_set_current_folder(dialog, directory);
                Gtk.gtk_file_chooser_set_do_overwrite_confirmation(dialog, 1);
            }
            if (Gtk.gtk_native_dialog_run(dialog) != -3) return;
            var filename = Gtk.gtk_file_chooser_get_filename(dialog);
            try
            {
                var path = Marshal.PtrToStringUTF8(filename) ?? string.Empty;
                if (action == 1 && !path.EndsWith(".iso", StringComparison.OrdinalIgnoreCase)) path += ".iso";
                Gtk.gtk_entry_set_text(entry, path);
            }
            finally { Gtk.g_free(filename); }
        }
        finally { Gtk.gtk_native_dialog_destroy(dialog); Gtk.g_object_unref(dialog); }
    }

    private bool ConfirmReplace(string path)
    {
        var dialog = Gtk.gtk_dialog_new();
        Gtk.gtk_window_set_title(dialog, "Replace existing file?");
        Gtk.gtk_window_set_transient_for(dialog, window);
        Gtk.gtk_window_set_modal(dialog, 1);
        Gtk.gtk_container_set_border_width(dialog, 16);
        AddLabel(Gtk.gtk_dialog_get_content_area(dialog), path);
        Gtk.gtk_dialog_add_button(dialog, "Cancel", -6);
        Gtk.gtk_dialog_add_button(dialog, "Replace", -3);
        Gtk.gtk_dialog_set_default_response(dialog, -6);
        Gtk.gtk_widget_show_all(dialog);
        try { return Gtk.gtk_dialog_run(dialog) == -3; }
        finally { Gtk.gtk_widget_destroy(dialog); }
    }

    private void Connect(IntPtr widget, string signal, Action action)
    {
        Gtk.SignalCallback callback = (_, _) =>
        {
            try { action(); }
            catch (Exception ex) { Gtk.gtk_label_set_text(status, ex.Message); }
        };
        callbacks.Add(callback);
        Gtk.g_signal_connect_data(widget, signal, Marshal.GetFunctionPointerForDelegate(callback), IntPtr.Zero, IntPtr.Zero, 0);
    }

    private static string Text(IntPtr entry) => Marshal.PtrToStringUTF8(Gtk.gtk_entry_get_text(entry)) ?? string.Empty;
    private static void Pack(IntPtr box, IntPtr widget) => Gtk.gtk_box_pack_start(box, widget, 0, 0, 0);
    private static IntPtr AddLabel(IntPtr box, string text)
    {
        var widget = Gtk.gtk_label_new(text);
        Gtk.gtk_label_set_xalign(widget, 0);
        Gtk.gtk_label_set_line_wrap(widget, 1);
        Pack(box, widget);
        return widget;
    }

    private static IntPtr AddPath(IntPtr layout, string heading, string buttonText, out IntPtr button)
    {
        AddLabel(layout, heading);
        var row = Gtk.gtk_box_new(0, 12);
        var entry = Gtk.gtk_entry_new();
        Gtk.gtk_box_pack_start(row, entry, 1, 1, 0);
        button = Gtk.gtk_button_new_with_label(buttonText);
        Pack(row, button);
        Pack(layout, row);
        return entry;
    }

    private sealed class QueuedProgress(ConcurrentQueue<IsoProgress> queue) : IProgress<IsoProgress>
    {
        public void Report(IsoProgress value) => queue.Enqueue(value);
    }
}
