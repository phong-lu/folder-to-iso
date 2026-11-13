namespace FolderToISO.Services;

public sealed record IsoProgress(double? Percent, string Message, string? Speed = null);

public static class IsoBuilder
{
    public static string SuggestedOutputPath(string source) => string.IsNullOrWhiteSpace(source)
        ? string.Empty : Path.TrimEndingDirectorySeparator(source.Trim()) + ".iso";

    public static async Task CreateAsync(string source, string destination, string label,
        IProgress<IsoProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("FolderToISO supports Windows and Linux.");
        if (string.IsNullOrWhiteSpace(source) || !Directory.Exists(source))
            throw new ArgumentException("Select an existing source folder.");
        if (string.IsNullOrWhiteSpace(destination) || !Path.IsPathFullyQualified(destination))
            throw new ArgumentException("Choose an absolute output file path.");
        source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
        destination = Path.GetFullPath(destination);
        if (!string.Equals(Path.GetExtension(destination), ".iso", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The output file must have an .iso extension.");
        var relative = Path.GetRelativePath(source, destination);
        if (!Path.IsPathFullyQualified(relative) && relative != ".." &&
            !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Save the ISO outside the source folder.");
        if (string.IsNullOrWhiteSpace(label) || label.Length > 32 || label.Any(char.IsControl) ||
            label.IndexOfAny(['\\', '/', ':', '*', '?', '"', '<', '>', '|']) >= 0)
            throw new ArgumentException("Use a volume label of 1-32 characters without path punctuation or control characters.");
        var outputDirectory = Path.GetDirectoryName(destination)!;
        if (!Directory.Exists(outputDirectory))
            throw new DirectoryNotFoundException("The output folder does not exist.");
        // Reject linked directory aliases that could place the output inside the source tree.
        RejectDirectoryLinks(new DirectoryInfo(outputDirectory));
        RejectDirectoryLinks(new DirectoryInfo(source));
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new(null, "Checking source files..."));
        await Task.Run(() =>
        {
            long total = 0;
            foreach (var file in EnumerateFiles(source, cancellationToken)) total = checked(total + file.Length);
            var drive = new DriveInfo(outputDirectory);
            if (drive.IsReady && drive.DriveFormat.Equals("FAT32", StringComparison.OrdinalIgnoreCase) && total >= uint.MaxValue)
                throw new IOException("FAT32 cannot store this image. Choose a filesystem supporting large files.");
            if (drive.IsReady && drive.AvailableFreeSpace < total)
                throw new IOException("The output drive does not have enough free space.");
        }, cancellationToken).ConfigureAwait(false);

        var temporary = Path.Combine(outputDirectory, $".foldertoiso-{Guid.NewGuid():N}.partial");
        try
        {
            if (OperatingSystem.IsWindows())
                await WindowsIsoWriter.CreateAsync(source, temporary, label, progress, cancellationToken).ConfigureAwait(false);
            else
                await LinuxIsoWriter.CreateAsync(source, temporary, label, progress, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, destination, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static void RejectDirectoryLinks(DirectoryInfo? directory)
    {
        for (; directory != null; directory = directory.Parent)
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Symbolic links and junctions are not supported: {directory.FullName}");
    }

    private static IEnumerable<FileInfo> EnumerateFiles(string folder, CancellationToken token)
    {
        foreach (var entry in new DirectoryInfo(folder).EnumerateFileSystemInfos())
        {
            token.ThrowIfCancellationRequested();
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Symbolic links and junctions are not supported: {entry.FullName}");
            if (OperatingSystem.IsLinux() && entry.Name.Length > 103)
                throw new IOException($"The Linux ISO writer supports names up to 103 characters. Shorten this name: {entry.FullName}");
            if (entry is DirectoryInfo child)
                foreach (var file in EnumerateFiles(child.FullName, token)) yield return file;
            else if (entry is FileInfo file) yield return file;
        }
    }
}
