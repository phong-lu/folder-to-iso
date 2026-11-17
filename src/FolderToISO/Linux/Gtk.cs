using System.Runtime.InteropServices;

namespace FolderToISO.Linux;

// Minimal GTK 3 ABI surface; Linux strings use UTF-8 and gboolean is a 32-bit int.
internal static class Gtk
{
    private const string Library = "libgtk-3.so.0";
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void SignalCallback(IntPtr widget, IntPtr data);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int DeleteCallback(IntPtr widget, IntPtr evt, IntPtr data);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int TimerCallback(IntPtr data);

    [DllImport("libgobject-2.0.so.0", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern nuint g_signal_connect_data(IntPtr instance, string signal, IntPtr handler, IntPtr data, IntPtr destroy, int flags);
    [DllImport("libgobject-2.0.so.0", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void g_object_unref(IntPtr instance);
    [DllImport("libglib-2.0.so.0", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void g_free(IntPtr memory);
    [DllImport("libglib-2.0.so.0", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint g_timeout_add(uint interval, TimerCallback callback, IntPtr data);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern int gtk_init_check(IntPtr argc, IntPtr argv);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern IntPtr gtk_window_new(int type);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern void gtk_window_set_title(IntPtr window, string title);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern void gtk_window_set_default_size(IntPtr window, int width, int height);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern void gtk_window_set_transient_for(IntPtr window, IntPtr parent);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern void gtk_window_set_modal(IntPtr window, int modal);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern void gtk_container_set_border_width(IntPtr container, uint width);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern void gtk_container_add(IntPtr container, IntPtr child);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern IntPtr gtk_box_new(int orientation, int spacing);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern void gtk_box_pack_start(IntPtr box, IntPtr child, int expand, int fill, uint padding);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern IntPtr gtk_label_new(string text);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern void gtk_label_set_text(IntPtr label, string text);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern void gtk_label_set_xalign(IntPtr label, float alignment);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern void gtk_label_set_line_wrap(IntPtr label, int wrap);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern IntPtr gtk_entry_new();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern void gtk_entry_set_text(IntPtr entry, string text);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern IntPtr gtk_entry_get_text(IntPtr entry);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern void gtk_entry_set_max_length(IntPtr entry, int length);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern IntPtr gtk_button_new_with_label(string label);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern void gtk_widget_set_sensitive(IntPtr widget, int sensitive);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern void gtk_widget_show_all(IntPtr widget);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern void gtk_widget_destroy(IntPtr widget);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern IntPtr gtk_progress_bar_new();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern void gtk_progress_bar_set_fraction(IntPtr progress, double fraction);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern void gtk_progress_bar_pulse(IntPtr progress);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern void gtk_main();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern void gtk_main_quit();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern IntPtr gtk_file_chooser_native_new(string title, IntPtr parent, int action, string accept, string cancel);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern int gtk_native_dialog_run(IntPtr dialog);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern void gtk_native_dialog_destroy(IntPtr dialog);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern IntPtr gtk_file_chooser_get_filename(IntPtr chooser);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern void gtk_file_chooser_set_current_name(IntPtr chooser, string name);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern int gtk_file_chooser_set_current_folder(IntPtr chooser, string folder);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern void gtk_file_chooser_set_do_overwrite_confirmation(IntPtr chooser, int confirm);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern IntPtr gtk_dialog_new();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern IntPtr gtk_dialog_get_content_area(IntPtr dialog);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern IntPtr gtk_dialog_add_button(IntPtr dialog, string text, int response);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern void gtk_dialog_set_default_response(IntPtr dialog, int response);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern int gtk_dialog_run(IntPtr dialog);
}
