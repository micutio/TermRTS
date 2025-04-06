using System.Numerics;
using System.Runtime.InteropServices;
using TermRTS.Event;
using TermRTS.Examples.Greenery.Command;
using TermRTS.Examples.Greenery.System;
using TermRTS.Examples.Greenery.Ui;
using TermRTS.Io;

namespace TermRTS.Examples.Greenery;

// TODO: Make world generation parameters changeable in-game.
// TODO: --> Create concept of a debug mode.

public class Greenery : IRunnableExample
{
    // private readonly ILog _log;
    private readonly CommandRunner _commandRunner = new();
    private readonly TextBox _textbox = new();

    #region IRunnableExample Members

    public void Run()
    {
        var previousTitle = "Powershell";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            previousTitle = Console.Title;
            Console.Title = "TermRTS - Greenery";
        }

        var seed = 0; //rng.Next();

        var worldWidth = 300;
        var worldHeight = 150;

        // Set up engine
        var renderer = new Renderer(worldWidth, worldHeight, _textbox);
        var core = new Core(renderer);

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

        var fovEntity = new EntityBase();
        var fovComponent = new FovComponent(fovEntity.Id, worldWidth, worldHeight);
        core.AddEntity(fovEntity);
        core.AddComponent(fovComponent);

        var droneEntity = new EntityBase();
        var droneComponent = new DroneComponent(droneEntity.Id, new Vector2(90, 10));
        core.AddEntity(droneEntity);
        core.AddComponent(droneComponent);

        var pathFindingSystem = new PathFindingSystem(worldWidth, worldHeight);
        core.AddSimSystem(pathFindingSystem);
        core.AddSimSystem(new FovSystem());

        var scheduler = new Scheduler(core);
        scheduler.AddEventSources(scheduler.ProfileEventReader);
        scheduler.AddEventSink(renderer, EventType.Profile);

        // Listen to commands
        scheduler.AddEventSink(renderer, EventType.Custom); // render option events
        scheduler.AddEventSources(_commandRunner.CommandEventReader);
        scheduler.AddEventSink(_commandRunner, EventType.Custom);
        scheduler.AddEventSink(pathFindingSystem, EventType.Custom);

        // Init input
        var input = new ConsoleInput(ConsoleKey.Escape);
        scheduler.AddEventSources(input.KeyEventReader);
        scheduler.AddEventSink(input, EventType.Shutdown);
        scheduler.AddEventSink(renderer, EventType.KeyInput);
        scheduler.AddEventSink(_textbox, EventType.KeyInput);
        scheduler.AddEventSources(_textbox.MessageEventReader);
        input.Run();

        var simulation = new Simulation(scheduler);
        simulation.EnableSerialization();

        // Graceful shutdown on canceling via CTRL+C.
        Console.CancelKeyPress += delegate(object? _, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            scheduler.EnqueueEvent((new PlainEvent(EventType.Shutdown), 0L));
            Console.Clear();
            Console.WriteLine("Simulation was shut down. Press a key to exit the program:");
        };

        // Automatically shut down after 10 minutes.
        // scheduler.EnqueueEvent((new PlainEvent(EventType.Shutdown), 1000 * 60 * 10));

        // Run it
        simulation.Run();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) Console.Title = previousTitle;
    }

    #endregion
}