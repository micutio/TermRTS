using log4net;
using TermRTS.IO;

namespace TermRTS.Examples.Greenery;

// TODO: Make world generation parameters changeable in-game.
// TODO: --> Create concept of a debug mode.

public class Greenery : IRunnableExample
{
    // private readonly ILog _log;
    
    public Greenery()
    {
        // _log = LogManager.GetLogger(GetType());
    }
    
    public void Run()
    {
        // _log.Info("~ Greenery ~");
        // var rng = new Random();
        var seed = 0; //rng.Next();
        
        var viewportWidth = Console.WindowWidth;
        var viewportHeight = Console.WindowHeight;
        var worldWidth = 300;
        var worldHeight = 150;
        // var worldWidth = viewportWidth;
        // var worldHeight = viewportHeight;
        // Set up engine
        var renderer = new Renderer(viewportWidth, viewportHeight, worldWidth, worldHeight);
        var core = new Core(renderer);
        
        // TODO: Move entity generation elsewhere.
        // TODO: Add 'enter text' component.
        var worldGen = new VoronoiWorld(50, seed);
        var worldEntity = new EntityBase();
        var worldComponent =
            new WorldComponent(
                worldEntity.Id,
                worldWidth,
                worldHeight,
                worldGen.Generate(worldWidth, worldHeight));
        core.AddEntity(worldEntity);
        core.AddComponent(worldComponent);
        
        var scheduler = new Scheduler(16, 16, core);
        scheduler.AddEventSources(scheduler.ProfileEventReader);
        scheduler.AddEventSink(renderer, EventType.Profile);
        
        // Init input
        var input = new ConsoleInput();
        scheduler.AddEventSources(input.KeyEventReader);
        scheduler.AddEventSink(input, EventType.Shutdown);
        scheduler.AddEventSink(renderer, EventType.KeyInput);
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
        
        // _log.Info("~ Greenery app shutdown ~");
    }
}