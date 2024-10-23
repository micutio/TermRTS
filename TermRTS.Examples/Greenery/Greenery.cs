using log4net;
using TermRTS.IO;

namespace TermRTS.Examples.Greenery;

public class Greenery : IRunnableExample
{
    private readonly ILog _log;
    
    public Greenery()
    {
        _log = LogManager.GetLogger(GetType());
    }
    
    public void Run()
    {
        _log.Info("~ Greenery ~");
        
        // Set up engine
        var renderer = new Renderer();
        var core = new Core(renderer);
        var scheduler = new Scheduler(16, 16, core);
        scheduler.AddEventSources(scheduler.ProfileEventReader);
        scheduler.AddEventSink(renderer, EventType.Profile);
        
        // Init input
        var input = new ConsoleInput();
        scheduler.AddEventSources(input.KeyEventReader);
        scheduler.AddEventSink(input, EventType.Shutdown);
        input.Run();
        
        // Graceful shutdown on canceling via CTRL+C.
        Console.CancelKeyPress += delegate(object? _, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            scheduler.EnqueueEvent((new PlainEvent(EventType.Shutdown), 0L));
        };
        
        // Automatically shut down after 10 minutes.
        scheduler.EnqueueEvent((new PlainEvent(EventType.Shutdown), 1000 * 60 * 10));
        
        // Run it
        scheduler.SimulationLoop();
        
        // After the app is terminated, clear the console.
        Console.Clear();
        
        _log.Info("~ Greenery app shutdown ~");
    }
}