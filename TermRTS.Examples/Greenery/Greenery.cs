using TermRTS.Event;
using TermRTS.Examples.Greenery.Command;
using TermRTS.Io;

namespace TermRTS.Examples.Greenery;

// TODO: Make world generation parameters changeable in-game.
// TODO: --> Create concept of a debug mode.

public class Greenery : IRunnableExample
{
    // private readonly ILog _log;
    private readonly CommandRunner _commandRunner = new();
    private readonly TextBox _textbox = new();
    
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
        var renderer = new Renderer(viewportWidth, viewportHeight, worldWidth, worldHeight, _textbox);
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
        
        // Listen to commands
        scheduler.AddEventSink(renderer, EventType.Custom); // render option events
        scheduler.AddEventSources(_commandRunner.CommandEventReader);
        scheduler.AddEventSink(_commandRunner, EventType.Custom);
        
        // Init input
        var input = new ConsoleInput();
        scheduler.AddEventSources(input.KeyEventReader);
        scheduler.AddEventSink(input, EventType.Shutdown);
        scheduler.AddEventSink(renderer, EventType.KeyInput);
        scheduler.AddEventSink(_textbox, EventType.KeyInput);
        scheduler.AddEventSources(_textbox.MessageEventReader);
        input.Run();
        
        
        // Graceful shutdown on canceling via CTRL+C.
        Console.CancelKeyPress += delegate(object? _, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            scheduler.EnqueueEvent((new PlainEvent(EventType.Shutdown), 0L));
        };
        
        // Automatically shut down after 10 minutes.
        // scheduler.EnqueueEvent((new PlainEvent(EventType.Shutdown), 1000 * 60 * 10));
        
        // Run it
        scheduler.SimulationLoop();
        
        // After the app is terminated, clear the console.
        Console.Clear();
    }
}