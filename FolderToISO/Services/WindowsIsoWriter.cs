using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace FolderToISO.Services;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
internal static class WindowsIsoWriter
{
    public static Task CreateAsync(string source, string destination, string label,
        IProgress<IsoProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        // All IMAPI objects are created, used and released on the same COM apartment.
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { Create(source, destination, label, progress, cancellationToken); completion.SetResult(); }
            catch (OperationCanceledException) { completion.SetCanceled(cancellationToken); }
            catch (Exception ex) { completion.SetException(ex); }
        }) { IsBackground = true, Name = "UDF image writer" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static void Create(string source, string destination, string label,
        IProgress<IsoProgress>? progress, CancellationToken token)
    {
        object? image = null, root = null, result = null, streamObject = null;
        IntPtr readCount = IntPtr.Zero;
        try
        {
            token.ThrowIfCancellationRequested();
            var drive = new DriveInfo(Path.GetPathRoot(destination)!);
            image = Activator.CreateInstance(Type.GetTypeFromProgID("IMAPI2FS.MsftFileSystemImage", true)!)!;
            dynamic fs = image;
            fs.FileSystemsToCreate = 4; // UDF only: ISO9660/Joliet impose smaller file limits.
            fs.UDFRevision = 0x201;
            fs.FreeMediaBlocks = 0; // Unlimited image, not the default 650 MB disc capacity.
            fs.VolumeName = label;
            root = fs.Root;
            progress?.Report(new(null, "Preparing UDF 2.01 image..."));
            ((dynamic)root).AddTree(source, false);
            token.ThrowIfCancellationRequested();
            result = fs.CreateResultImage();
            streamObject = ((dynamic)result).ImageStream;
            var stream = (IStream)streamObject;
            stream.Stat(out var stat, 1);
            long imageLength = stat.cbSize;
            if (imageLength <= 0) throw new IOException("IMAPI returned an empty image.");
            if (drive.IsReady && drive.AvailableFreeSpace < imageLength)
                throw new IOException("The output drive has insufficient space for the image and its metadata.");
            if (drive.IsReady && drive.DriveFormat.Equals("FAT32", StringComparison.OrdinalIgnoreCase) && imageLength > uint.MaxValue)
                throw new IOException("This ISO exceeds FAT32's file size limit. Choose NTFS or exFAT.");
            stream.Seek(0, 0, IntPtr.Zero);
            readCount = Marshal.AllocHGlobal(sizeof(int));
            var buffer = new byte[1024 * 1024];
            long written = 0;
            long lastReportedBytes = 0;
            var update = System.Diagnostics.Stopwatch.StartNew();
            using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       buffer.Length, FileOptions.SequentialScan))
            {
                while (written < imageLength)
                {
                    token.ThrowIfCancellationRequested();
                    int requested = (int)Math.Min(buffer.Length, imageLength - written);
                    Marshal.WriteInt32(readCount, 0);
                    stream.Read(buffer, requested, readCount);
                    int count = Marshal.ReadInt32(readCount);
                    if (count <= 0 || count > requested) throw new IOException("Unexpected end of the image stream.");
                    output.Write(buffer, 0, count);
                    written += count;
                    if (update.ElapsedMilliseconds >= 150 || written == imageLength)
                    {
                        var mebibytesPerSecond = (written - lastReportedBytes) / 1_048_576.0 /
                            Math.Max(update.Elapsed.TotalSeconds, 0.000001);
                        var speed = mebibytesPerSecond > 1024
                            ? $"{mebibytesPerSecond / 1024:F1} GiB/s"
                            : $"{mebibytesPerSecond:F1} MiB/s";
                        progress?.Report(new(written * 100.0 / imageLength,
                            $"Writing ISO: {written / 1073741824.0:F2} / {imageLength / 1073741824.0:F2} GiB", speed));
                        lastReportedBytes = written;
                        update.Restart();
                    }
                }
                output.Flush(true);
            }
            token.ThrowIfCancellationRequested();
        }
        finally
        {
            if (readCount != IntPtr.Zero) Marshal.FreeHGlobal(readCount);
            Release(streamObject); Release(result); Release(root); Release(image);
        }
    }

    private static void Release(object? value)
    {
        if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
    }
}
