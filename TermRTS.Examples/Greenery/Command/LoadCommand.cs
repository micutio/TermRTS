using TermRTS.Algorithms;
using TermRTS.Event;
using TermRTS.Storage;

using System.Runtime.InteropServices;

namespace TermRTS.Examples.Greenery.Command;

public sealed class LoadCommand : ICommand
{
    #region ICommand Members

    private const string Name = "load";
    private const string Description = "loads the previously saved game.";
    private const string Usage = "load";

    private const string ErrorTooManyArgs = "< Too many arguments!";

    public string GetName() => Name;

    public string GetDescription() => Description;

    public string GetUsage() => Usage;

    public CommandInitResult Init(IReadOnlyList<Token> tokens)
    {
        if (tokens.Count > 1) return new CommandInitResult(false, ErrorTooManyArgs);

        return new CommandInitResult(true, string.Empty);
    }

    public void Execute(
            ulong timeStepSizeMs,
            in IReadonlyStorage storage,
            in List<ScheduledEvent> emittedEvents)
    {
        var loadEvent = new Persist(PersistenceOption.Load, GetFilePath());
        emittedEvents.Add(ScheduledEvent.From(loadEvent));
    }

    #endregion

    #region Public Members

    public static string GetFilePath()
    {
        // TODO: Use XDG defaults
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "c:/Users/WA_MICHA/savegame.json"
            : "/home/michael/savegame.json";
    }

    #endregion
}