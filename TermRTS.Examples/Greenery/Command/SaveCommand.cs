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

    /// <inheritdoc/>
    public string GetName()
    {
        return Name;
    }

    /// <inheritdoc/>
    public string GetDescription()
    {
        return Description;
    }

    /// <inheritdoc/>
    public string GetUsage()
    {
        return Usage;
    }

    /// <inheritdoc/>
    public CommandInitResult CreateNew(IReadOnlyList<Token> tokens)
    {
        if (tokens.Count > 1) return new CommandInitResult(null, ErrorTooManyArgs);

        return new CommandInitResult(this, string.Empty);
    }

    /// <inheritdoc/>
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