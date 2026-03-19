using System.Numerics;
using TermRTS.Event;
using TermRTS.Examples.Greenery.Command;
using TermRTS.Examples.Greenery.Event;
using TermRTS.Examples.Greenery.System;
using TermRTS.Examples.Greenery.Ui;
using TermRTS.Io;
using TermRTS.Shared.Harness;
using TermRTS.Shared.World;

namespace TermRTS.Examples.Greenery;

// TODO: Make world generation parameters changeable in-game.
// TODO: --> Create concept of a debug mode.

public class Greenery : IRunnableExample
{

    // private readonly ILog _log;
    private CommandRunner? _commandRunner;

    #region IRunnableExample Members

    public void Run()
    {
        var previousTitle = ConsoleTitleHelper.SaveAndSet("TermRTS - Greenery");

        var seed = 0; //rng.Next();

        // Set up engine
        var core = new Core();

        var worldGen = new VoronoiWorld(50, seed);
        var worldEntity = new EntityBase();
        var worldComponent =
            new WorldComponent(
                worldEntity.Id,
                DefaultWorldDimensions.Width,
                DefaultWorldDimensions.Height,
                worldGen.Generate(DefaultWorldDimensions.Width, DefaultWorldDimensions.Height));
        core.AddEntity(worldEntity);
        core.AddComponent(worldComponent);

        var fovEntity = new EntityBase();
        var fovComponent = new FovComponent(fovEntity.Id, DefaultWorldDimensions.Width,
            DefaultWorldDimensions.Height);
        core.AddEntity(fovEntity);
        core.AddComponent(fovComponent);

        var droneEntity = new EntityBase();
        var droneComponent = new DroneComponent(droneEntity.Id, new Vector2(110, 40));
        core.AddEntity(droneEntity);
        core.AddComponent(droneComponent);

        var pathFindingSystem = new PathFindingSystem(DefaultWorldDimensions.Width,
            DefaultWorldDimensions.Height);
        core.AddSimSystem(pathFindingSystem);
        core.AddSimSystem(new FovSystem());

        var scheduler = new Scheduler(core);
        var renderer = new Renderer(scheduler.EventQueue, DefaultWorldDimensions.Width,
            DefaultWorldDimensions.Height);
        core.Renderer = renderer;
        scheduler.AddEventSink(renderer, typeof(Profile));

        // Listen to commands
        _commandRunner = new CommandRunner(scheduler.EventQueue);
        scheduler.AddEventSink(renderer, typeof(MapRenderMode)); // render option events
        scheduler.AddEventSink(_commandRunner, typeof(Event.Command));
        scheduler.AddEventSink(pathFindingSystem, typeof(Move));

        // Init input
        var input = new ConsoleInput(scheduler.EventQueue, ConsoleKey.Escape);
        scheduler.AddEventSink(input, typeof(Shutdown));
        scheduler.AddEventSink(renderer, typeof(ConsoleKeyInfo));
        scheduler.AddEventSink(renderer.LogArea, typeof(SystemLog));
        input.Run();

        var simulation = new Simulation(scheduler);
        simulation.EnableSerialization();
        simulation.IsSystemLogEnabled = true;

        GracefulShutdown.RegisterCancelHandler(scheduler.EventQueue);

        // Run it
        simulation.Run();

        ConsoleTitleHelper.Restore(previousTitle);
    }

    #endregion
}