using System.Runtime.InteropServices;

namespace TermRTS.Shared.Harness;

/// <summary>
///     Saves and restores <see cref="Console.Title" /> on Windows.
/// </summary>
public static class ConsoleTitleHelper
{
    public static string SaveAndSet(string newTitle)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return string.Empty;

        var previous = Console.Title;
        Console.Title = newTitle;
        return previous;
    }

    public static void Restore(string? previousTitle)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || previousTitle is null)
            return;

        Console.Title = previousTitle;
    }
}
