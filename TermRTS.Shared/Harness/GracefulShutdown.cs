using TermRTS.Event;

namespace TermRTS.Shared.Harness;

/// <summary>
///     Registers CTRL+C to enqueue <see cref="Shutdown" /> instead of terminating the process.
/// </summary>
public static class GracefulShutdown
{
    public static void RegisterCancelHandler(
        SchedulerEventQueue queue,
        bool clearConsoleOnCancel = true,
        string exitPrompt = "Simulation was shut down. Press a key to exit the program:")
    {
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            queue.EnqueueEvent(ScheduledEvent.From(new Shutdown()));
            if (clearConsoleOnCancel)
            {
                Console.Clear();
                Console.WriteLine(exitPrompt);
            }
        };
    }
}
