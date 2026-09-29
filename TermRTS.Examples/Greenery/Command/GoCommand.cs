using TermRTS.Algorithms;
using TermRTS.Event;
using TermRTS.Storage;
using TermRTS.Examples.Greenery.Event;
using System.Numerics;

namespace TermRTS.Examples.Greenery.Command;

public sealed class GoCommand : ICommand
{
    #region Public Fields

    public float X { get; set; }

    public float Y { get; set; }

    #endregion


    #region ICommand Members

    private const string Name = "go";
    private const string Description = "Move the drone somewhere.";
    private const string Usage = "go <x> <y>";

    private const string ErrorTooFewArgs = "< Too few arguments!";
    private const string ErrorTooManyArgs = "< Too many arguments!";
    private const string ErrorInvalidArgs = "Error: both following arguments must be numbers";

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
        if (tokens.Count < 3) return new CommandInitResult(null, ErrorTooFewArgs);

        if (tokens.Count > 3) return new CommandInitResult(null, ErrorTooManyArgs);

        if (tokens[1].TokenType != TokenType.Number || tokens[2].TokenType != TokenType.Number)
            return new CommandInitResult(null, ErrorInvalidArgs);

        X = Convert.ToSingle(tokens[1].Literal);
        Y = Convert.ToSingle(tokens[2].Literal);

        var newCmd = new GoCommand
        {
            X = X,
            Y = Y
        };
        return new CommandInitResult(newCmd, string.Empty);
    }

    /// <inheritdoc/>
    public void Execute(
        ulong timeStepSizeMs,
        in IReadonlyStorage storage,
        in List<ScheduledEvent> emittedEvents)
    {
        // TODO: Remove stupid hardcoded entity ID.
        emittedEvents.Add(ScheduledEvent.From(new Move(31, new Vector2(X, Y))));
    }

    #endregion
}