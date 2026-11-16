using System.Diagnostics;
using System.Globalization;

namespace FolderToISO.Services;

internal static class MacIsoWriter
{
    internal static async Task CreateAsync(string source, string destination, string label,
        IProgress<IsoProgress>? progress, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo("/usr/bin/hdiutil")
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            CreateNoWindow = true
        };
        // ArgumentList preserves spaces, Unicode and shell punctuation literally.
        // makehybrid does not support -puppetstrings. Most work is indeterminate.
        foreach (var argument in new[] { "makehybrid", "-udf", "-udf-volume-name", label, "-o", destination, source })
            start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        process.Start();
        process.StandardInput.Close();
        using var registration = token.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(); }
            catch (InvalidOperationException) { }
        });
        progress?.Report(new(null, "Preparing UDF image..."));
        var errors = ReadAsync(process.StandardError);
        var output = ReadAsync(process.StandardOutput);
        // Reap the child and drain both pipes before cleaning up a cancelled image.
        await process.WaitForExitAsync().ConfigureAwait(false);
        var diagnostics = await Task.WhenAll(errors, output).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        if (process.ExitCode != 0)
            throw new IOException($"hdiutil failed (exit {process.ExitCode}): {string.Join(Environment.NewLine, diagnostics)}");
        if (!File.Exists(destination) || new FileInfo(destination).Length == 0)
            throw new IOException("hdiutil did not produce an image.");
        progress?.Report(new(100, "ISO image complete."));

        async Task<string> ReadAsync(StreamReader reader)
        {
            var tail = new Queue<string>();
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (tail.Count == 20) tail.Dequeue();
                tail.Enqueue(line.Length > 4096 ? line[^4096..] : line);
                if ((line.StartsWith("PERCENT:", StringComparison.Ordinal) ||
                     line.StartsWith("PERCENTAGE:", StringComparison.Ordinal)) &&
                    double.TryParse(line[(line.IndexOf(':') + 1)..].Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var percent))
                    progress?.Report(new(percent < 0 ? null : Math.Clamp(percent, 0, 100), "Writing ISO..."));
            }
            return string.Join(Environment.NewLine, tail);
        }
    }
}
