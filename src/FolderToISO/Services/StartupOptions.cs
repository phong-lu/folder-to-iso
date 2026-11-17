namespace FolderToISO.Services;

public sealed record StartupOptions(string? SourceFolder, string? Error)
{
    public static StartupOptions Parse(string[] arguments)
    {
        if (arguments.Length == 0) return new(null, null);
        if (arguments.Length == 2 && arguments[0] == "--source" &&
            !string.IsNullOrWhiteSpace(arguments[1]) && Path.IsPathFullyQualified(arguments[1]))
            return new(arguments[1], null);
        return new(null, "Usage: FolderToISO --source <absolute folder path>");
    }
}
