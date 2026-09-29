using System.Text.Json.Serialization;
using TermRTS.Algorithms;
using TermRTS.Event;
using TermRTS.Storage;

namespace TermRTS.Examples.Greenery.Command;

public readonly record struct CommandInitResult(ICommand? cmd, string LogMsg);

// TODO: Implement help program.

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$command")]
[JsonDerivedType(typeof(GoCommand), "go")]
[JsonDerivedType(typeof(LoadCommand), "load")]
[JsonDerivedType(typeof(SaveCommand), "save")]
public interface ICommand
{
    /// <summary>
    ///     Return the name of the command.
    ///     This is the same term which is used to invoke it.
    /// </summary>
    /// <returns>
    ///     Name of the program as string.
    /// </returns>
    string GetName();

    /// <summary>
    ///     Return the description of the command.
    ///     It should describe what it does.
    /// </summary>
    /// <returns>
    ///     Description of the program as string.
    /// </returns>
    string GetDescription();

    /// <summary>
    ///     Return the Usage of the command.
    ///     This should describe all parameters and flags that can be used with the command.
    /// </summary>
    /// <returns>
    ///     Usage of the program as string.
    /// </returns>
    string GetUsage();

    /// <summary>
    ///     Initialise the command with the given list of tokens.
    ///     The first item is the command name itself, followed by arguments
    ///     and parameters provided by the user.
    /// </summary>
    /// <returns>
    ///     <see cref="CommandInitResult" /> consisting of the following:
    ///     1. A new instance of the command if the command has been successfully initialised,
    ///     <see Langword="null" /> otherwise.
    ///     2. Optional log message with feedback to the user.
    /// </returns>
    CommandInitResult CreateNew(IReadOnlyList<Token> tokens);

    /// <summary>
    ///     Executes the command against the game state.
    /// </summary>
    /// <param name="timeStepSizeMs">
    ///     Time step size in [milliseconds], for physical calculations and stuff.
    /// </param>
    /// <param name="storage">
    ///     Reference to storage for access to all game components
    /// </param>
    /// <param name="emittedEvents">
    ///     Outbox for putting events to be emitted after this has executed.
    /// </param>
    void Execute(
        ulong timeStepSizeMs,
        in IReadonlyStorage storage,
        in List<ScheduledEvent> emittedEvents);
}