using System.Text.Json.Serialization;
using TermRTS.Algorithms;
using TermRTS.Event;
using TermRTS.Storage;

namespace TermRTS.Examples.Greenery.Command;

public readonly record struct CommandInitResult(bool IsSuccess, string LogMsg);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$command")]
[JsonDerivedType(typeof(GoCommand), "go")]
[JsonDerivedType(typeof(LoadCommand), "load")]
[JsonDerivedType(typeof(SaveCommand), "save")]
public interface ICommand
{
    string GetName();

    string GetDescription();

    string GetUsage();

    /// <summary>
    ///     Initialise the command with the given list of tokens.
    ///     The first item is the command name itself, followed by arguments
    ///     and parameters provided by the user.
    /// </summary>
    /// <returns> 
    ///     <see cref="CommandInitResult"/> consisting of the following:
    ///     1. <code>true</code> if the command has been successfully initialised,
    ///     <code>false</code> otherwise.
    ///     2. Optional log message with feedback to the user.
    /// </returns>
    CommandInitResult Init(IReadOnlyList<Token> tokens);

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