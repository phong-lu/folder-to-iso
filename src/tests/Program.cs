using FolderToISO.Services;
using System.Text;

try
{
var launchFolder = Path.Combine(Path.GetFullPath("artifacts"), "folder with spaces and caf\u00e9");
var launch = StartupOptions.Parse(["--source", launchFolder]);
if (launch.SourceFolder != launchFolder || launch.Error != null ||
    IsoBuilder.SuggestedOutputPath(launch.SourceFolder) != launchFolder + ".iso")
    throw new Exception("Explorer folder activation did not preserve the source and output paths.");
if (StartupOptions.Parse([]) != new StartupOptions(null, null) ||
    StartupOptions.Parse(["--source"]).Error == null ||
    StartupOptions.Parse(["--source", "relative-folder"]).Error == null ||
    StartupOptions.Parse(["--source", launchFolder, "extra-folder"]).Error == null)
    throw new Exception("Invalid startup arguments were accepted.");
Console.WriteLine("PASS: Explorer startup arguments, spaces, Unicode and automatic ISO naming.");
var testRoot = Path.Combine(Path.GetFullPath("artifacts"), "integration-" + Guid.NewGuid().ToString("N"));
var source = Path.Combine(testRoot, "source");
Directory.CreateDirectory(Path.Combine(source, "Nested"));
await File.WriteAllTextAsync(Path.Combine(source, "Nested", "spaces and 'quotes'.txt"), "Names with spaces and quotes");
await File.WriteAllTextAsync(Path.Combine(source, "Nested", "caf\u00e9.txt"), "Unicode file name");
await File.WriteAllTextAsync(Path.Combine(source, "Nested", "hello.txt"), "Hello from FolderToISO");
Directory.CreateDirectory(Path.Combine(source, "Empty"));
var deep = source;
for (int depth = 0; depth < 10; depth++) deep = Path.Combine(deep, "Level" + depth);
Directory.CreateDirectory(deep);
await File.WriteAllTextAsync(Path.Combine(deep, "deep.txt"), "Deep directory contents");
string iso = Path.Combine(testRoot, "test.iso");
await IsoBuilder.CreateAsync(source, iso, "FolderToISO_Test");
using (var stream = File.OpenRead(iso))
{
    var bytes = new byte[Math.Min(stream.Length, 1024 * 1024)];
    stream.ReadExactly(bytes);
    var descriptors = Encoding.ASCII.GetString(bytes);
    if (!descriptors.Contains("NSR03") && !descriptors.Contains("NSR02")) throw new Exception("UDF descriptor missing.");
}
Console.WriteLine("PASS: UDF image creation, nested files and empty directories.");
try
{
    await IsoBuilder.CreateAsync(source, Path.Combine(source, "bad.iso"), "Test");
    throw new Exception("Output inside source was accepted.");
}
catch (ArgumentException) { Console.WriteLine("PASS: Output inside source rejected."); }
var original = await File.ReadAllBytesAsync(iso);
using (var cts = new CancellationTokenSource())
{
    cts.Cancel();
    try { await IsoBuilder.CreateAsync(source, iso, "Test", cancellationToken: cts.Token); throw new Exception("Cancellation ignored."); }
    catch (OperationCanceledException) { }
}
var afterCancellation = await File.ReadAllBytesAsync(iso);
if (!original.SequenceEqual(afterCancellation)) throw new Exception("Cancellation changed existing ISO.");
Console.WriteLine("PASS: Cancellation preserves existing output.");
if (OperatingSystem.IsLinux()) await CheckLinuxFailures(source, iso, original, testRoot);
if (args.Contains("--large"))
{
    string large = Path.Combine(source, "large.bin");
    using (var file = File.Create(large))
    {
        if (OperatingSystem.IsWindows() && !Native.DeviceIoControl(file.SafeFileHandle, 0x900C4, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
            throw new System.ComponentModel.Win32Exception();
        file.SetLength((1L << 32) + 1024 * 1024);
        file.Position = (1L << 32) + 123;
        file.Write(Encoding.ASCII.GetBytes("FolderToISO marker beyond 4 GiB"));
    }
    await IsoBuilder.CreateAsync(source, iso, "Large_Test");
    if (new FileInfo(iso).Length <= new FileInfo(large).Length) throw new Exception("Image is too small.");
        using var image = File.OpenRead(iso);
    image.Position = 1L << 32;
    var tail = new byte[checked((int)(image.Length - image.Position))];
    image.ReadExactly(tail);
    if (!Encoding.ASCII.GetString(tail).Contains("FolderToISO marker beyond 4 GiB"))
        throw new Exception("Data beyond 4 GiB was not preserved.");
    Console.WriteLine("PASS: File larger than 4 GiB and its data beyond the 4 GiB boundary preserved in image.");
}
Console.WriteLine($"Image for mount verification: {iso}");
await File.WriteAllTextAsync(Path.Combine("artifacts", "last-integration-image.txt"), iso);
if (Directory.EnumerateFiles(testRoot, ".foldertoiso-*.partial").Any()) throw new Exception("Temporary images remain.");
if (IsoBuilder.SuggestedOutputPath(source + Path.DirectorySeparatorChar) != source + ".iso" ||
    IsoBuilder.SuggestedOutputPath(" ") != string.Empty)
    throw new Exception("Automatic ISO path naming failed.");
Console.WriteLine("PASS: Automatic output naming and temporary-file cleanup.");


}
catch (Exception ex)
{
    Console.Error.WriteLine($"FAIL: {ex.Message}");
    Environment.ExitCode = 1;
}

static async Task CheckLinuxFailures(string source, string iso, byte[] original, string testRoot)
{
    var tools = Path.Combine(testRoot, "fake tools");
    Directory.CreateDirectory(tools);
    var tool = Path.Combine(tools, "genisoimage");
    var previousPath = Environment.GetEnvironmentVariable("PATH");
    try
    {
        Environment.SetEnvironmentVariable("PATH", tools);
        try { await IsoBuilder.CreateAsync(source, iso, "Test"); throw new Exception("Missing tool was accepted."); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Install genisoimage")) { }
        Environment.SetEnvironmentVariable("PATH", tools + Path.PathSeparator + previousPath);
        await File.WriteAllTextAsync(tool, "#!/bin/sh\nwhile [ \"$#\" -gt 0 ]; do\n if [ \"$1\" = '-o' ]; then shift; printf 'partial image' > \"$1\"; break; fi\n shift\ndone\necho 'simulated writer failure' >&2\nexit 7\n");
        if (OperatingSystem.IsLinux()) File.SetUnixFileMode(tool, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try { await IsoBuilder.CreateAsync(source, iso, "Test"); throw new Exception("Failed tool was accepted."); }
        catch (IOException ex) when (ex.Message.Contains("exit 7") && ex.Message.Contains("simulated writer failure")) { }
        await File.WriteAllTextAsync(tool, "#!/bin/sh\nsleep 30\n");
        using var cts = new CancellationTokenSource();
        var progress = new InlineProgress(p => { if (p.Message == "Preparing UDF image...") cts.Cancel(); });
        try
        {
            await IsoBuilder.CreateAsync(source, iso, "Test", progress, cts.Token).WaitAsync(TimeSpan.FromSeconds(10));
            throw new Exception("Running Linux writer ignored cancellation.");
        }
        catch (OperationCanceledException) { }
        var afterFailure = await File.ReadAllBytesAsync(iso);
        if (!original.SequenceEqual(afterFailure)) throw new Exception("Backend failure changed existing output.");
        if (Directory.EnumerateFiles(testRoot, ".foldertoiso-*.partial").Any()) throw new Exception("Backend failure left temporary files.");
        Console.WriteLine("PASS: Missing Linux tool, backend errors, running-process cancellation and output preservation.");
    }
    finally { Environment.SetEnvironmentVariable("PATH", previousPath); }
    var alias = Path.Combine(testRoot, "source-alias");
    Directory.CreateSymbolicLink(alias, source);
    try
    {
        try { await IsoBuilder.CreateAsync(source, Path.Combine(alias, "bad.iso"), "Test"); throw new Exception("Output alias inside source was accepted."); }
        catch (IOException ex) when (ex.Message.Contains("Symbolic links")) { }
    }
    finally { Directory.Delete(alias); }
    Console.WriteLine("PASS: Output through a symbolic-link alias rejected.");
    var longName = Path.Combine(source, new string('a', 104));
    await File.WriteAllTextAsync(longName, "Must not be silently renamed");
    try
    {
        try { await IsoBuilder.CreateAsync(source, iso, "Test"); throw new Exception("Unsupported long name was accepted."); }
        catch (IOException ex) when (ex.Message.Contains("103 characters")) { }
    }
    finally { File.Delete(longName); }
    Console.WriteLine("PASS: Unsupported Linux filename length rejected before writing.");
}

internal sealed class InlineProgress(Action<IsoProgress> report) : IProgress<IsoProgress>
{
    public void Report(IsoProgress value) => report(value);
}

internal static class Native
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    internal static extern bool DeviceIoControl(Microsoft.Win32.SafeHandles.SafeFileHandle handle,
        uint code, IntPtr input, uint inputSize, IntPtr output, uint outputSize, out uint returned, IntPtr overlapped);
}

