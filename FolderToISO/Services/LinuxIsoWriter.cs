using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace FolderToISO.Services;

internal static class LinuxIsoWriter
{
    internal static async Task CreateAsync(string source, string destination, string label,
        IProgress<IsoProgress>? progress, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo("genisoimage")
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        // UDF carries the full size of files over 4 GiB; Rock Ridge/Joliet improve compatibility.
        foreach (var argument in new[] { "-udf", "-allow-limited-size", "-r", "-J", "-joliet-long", "-D", "-input-charset", "utf-8", "-V", label, "-o", destination, source })
            start.ArgumentList.Add(argument);
        start.Environment["LC_ALL"] = "C.UTF-8";
        using var process = new Process { StartInfo = start };
        try { process.Start(); }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            throw new InvalidOperationException("Install genisoimage to create ISO files on Linux (for example: sudo apt install genisoimage).", ex);
        }
        using var cancellation = token.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        });
        progress?.Report(new(null, "Preparing UDF image..."));
        var diagnostics = new Queue<string>();
        var errors = ReadErrorsAsync();
        var output = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
        await process.WaitForExitAsync().ConfigureAwait(false);
        await Task.WhenAll(errors, output).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        if (process.ExitCode != 0)
            throw new IOException($"genisoimage failed (exit {process.ExitCode}): {string.Join(Environment.NewLine, diagnostics)}");
        if (!File.Exists(destination) || new FileInfo(destination).Length == 0)
            throw new IOException("genisoimage did not produce an image.");
        progress?.Report(new(100, "ISO image complete."));

        async Task ReadErrorsAsync()
        {
            while (await process.StandardError.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (diagnostics.Count == 20) diagnostics.Dequeue();
                diagnostics.Enqueue(line);
                var percentIndex = line.IndexOf('%');
                if (percentIndex > 0 && double.TryParse(line[..percentIndex].Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var percent))
                    progress?.Report(new(Math.Clamp(percent, 0, 100), $"Writing ISO: {percent:F1}%"));
            }
        }
    }
}
