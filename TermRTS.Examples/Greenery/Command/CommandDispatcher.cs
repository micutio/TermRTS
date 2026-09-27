using TermRTS.Algorithms;
using TermRTS.Event;
using TermRTS.Examples.Greenery.Event;

namespace TermRTS.Examples.Greenery.Command;

/// <summary>
///     CommandDispatcher receives a string via event, parses it into a list of tokens
///     and finally executes the respective command.
/// </summary>
/// <param name="evtQueue">Reference to the <see cref="Scheduler" />'s event queue</param>
public readonly record struct CommandDispatcher(
        SchedulerEventQueue evtQueue,
        Dictionary<string, ICommand> cmdRegistry) : IEventSink
{
    // Replies
    private const string ErrorEmptyCmd = "< Cannot run empty command";
    private const string ErrorNoIdentifier = "< Command must start with an identifier!";
    private const string ErrorUnknownCmd = "< Unknown command!";

    #region IEventSink Members

    /// <inheritdoc />
    public void ProcessEvent(IEvent evt)
    {
        if (evt is not Event<CommandInput>(var command)) return;

        var cmdStr = new string(command.Cmd);
        // Log reception of the command
        emitLog(cmdStr);

        var tokens = new Scanner(command.Cmd).ScanTokens();

        if (tokens.Count == 0)
        {
            emitLog(ErrorEmptyCmd);
            return;
        }

        var firstToken = tokens[0];
        if (firstToken.TokenType != TokenType.Identifier)
        {
            emitLog(ErrorNoIdentifier);
            return;
        }

        var cmdName = firstToken.Lexeme;
        if (cmdName == null)
        {
            emitLog(ErrorUnknownCmd);
            return;
        }

        if (!cmdRegistry.TryGetValue(cmdName, out var cmd))
        {
            emitLog(ErrorUnknownCmd);
            return;
        }

        evtQueue.EnqueueEvent(ScheduledEvent.From(cmd));
    }

    #endregion

    private void emitLog(string msg)
    {
        evtQueue.EnqueueEvent(ScheduledEvent.From(new SystemLog(msg)));
    }

}