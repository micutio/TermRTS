using TermRTS.Algorithms;
using TermRTS.Event;
using TermRTS.Examples.Greenery.Event;

namespace TermRTS.Examples.Greenery.Command;

/// <summary>
///     CommandDispatcher receives a string via event, parses it into a list of tokens
///     and finally executes the respective command.
/// </summary>
/// <param name="EvtQueue">Reference to the <see cref="Scheduler" />'s event queue</param>
public readonly record struct CommandDispatcher(
    SchedulerEventQueue EvtQueue,
    Dictionary<string, ICommand> CmdRegistry) : IEventSink
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
        EmitLog(cmdStr);

        var tokens = ToTokens(command);
        if (tokens == null) return;

        var cmd = ToCommand(tokens);
        if (cmd == null) return;

        EvtQueue.EnqueueEvent(ScheduledEvent.From(cmd));
    }

    #endregion

    #region Private Members

    private void EmitLog(string msg)
    {
        EvtQueue.EnqueueEvent(ScheduledEvent.From(new SystemLog(msg)));
    }

    private IReadOnlyList<Token>? ToTokens(CommandInput input)
    {
        var tokens = new Scanner(input.Cmd).ScanTokens();

        if (tokens.Count != 0) return tokens;

        EmitLog(ErrorEmptyCmd);
        return null;
    }

    private ICommand? ToCommand(IReadOnlyList<Token> tokens)
    {
        var (tokenType, cmdName, _) = tokens[0];
        if (tokenType != TokenType.Identifier)
        {
            EmitLog(ErrorNoIdentifier);
            return null;
        }

        if (cmdName == null || !CmdRegistry.TryGetValue(cmdName, out var cmd))
        {
            EmitLog(ErrorUnknownCmd);
            return null;
        }

        var result = cmd.CreateNew(tokens);
        if (!string.IsNullOrEmpty(result.LogMsg)) EmitLog(result.LogMsg);

        return result.cmd;
    }

    #endregion
}