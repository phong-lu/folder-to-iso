namespace FolderToISO.Services;

public static class IsoCommand
{
    public static async Task<int> RunAsync(string[] arguments)
    {
        if (arguments.Length is not (4 or 5) || arguments[0] != "--create" ||
            (arguments.Length == 5 && arguments[4] != "--replace"))
        {
            Console.Error.WriteLine("Usage: FolderToISO --create <source> <output.iso> <label> [--replace]");
            return 2;
        }
        try
        {
            await IsoBuilder.CreateAsync(arguments[1], arguments[2], arguments[3], overwrite: arguments.Length == 5);
            Console.WriteLine($"Created {arguments[2]}");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
}
