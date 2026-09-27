using TermRTS.Algorithms;
using TermRTS.Event;
using TermRTS.Storage;

using TermRTS.Examples.Greenery.Event;
using System.Numerics;

namespace TermRTS.Examples.Greenery.Command;

public sealed class GoCommand : ICommand
{
    #region Private Fields

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

    public string GetName() => Name;

    public string GetDescription() => Description;

    public string GetUsage() => Usage;

    public CommandInitResult Init(IReadOnlyList<Token> tokens)
    {
        if (tokens.Count < 3) return new CommandInitResult(false, ErrorTooFewArgs);

        if (tokens.Count > 3) return new CommandInitResult(false, ErrorTooManyArgs);

        if (tokens[1].TokenType != TokenType.Number || tokens[2].TokenType != TokenType.Number)
            return new CommandInitResult(false, ErrorInvalidArgs);

        X = Convert.ToSingle(tokens[1].Literal);
        Y = Convert.ToSingle(tokens[2].Literal);
        return new CommandInitResult(true, string.Empty);

    }

    public void Execute(
            ulong timeStepSizeMs,
            in IReadonlyStorage storage,
            in List<ScheduledEvent> emittedEvents)
    {
        emittedEvents.Add(ScheduledEvent.From(new Move(3, new Vector2(X, Y))));
    }

    #endregion
}