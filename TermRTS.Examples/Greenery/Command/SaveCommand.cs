using TermRTS.Algorithms;
using TermRTS.Event;
using TermRTS.Storage;

namespace TermRTS.Examples.Greenery.Command;

public sealed class SaveCommand : ICommand
{
    #region ICommand Members

    private const string Name = "save";
    private const string Description = "saves the current game.";
    private const string Usage = "save";

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
        var saveEvent = new Persist(PersistenceOption.Save, LoadCommand.GetFilePath());
        emittedEvents.Add(ScheduledEvent.From(saveEvent));
    }

    #endregion

}