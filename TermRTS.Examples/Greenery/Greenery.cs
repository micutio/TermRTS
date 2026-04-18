using System.Numerics;
using System.Runtime.InteropServices;
using TermRTS.Event;
using TermRTS.Examples.Greenery.Command;
using TermRTS.Examples.Greenery.Ecs.Component;
using TermRTS.Examples.Greenery.Event;
using TermRTS.Examples.Greenery.System;
using TermRTS.Examples.Greenery.Ui;
using TermRTS.Examples.Greenery.WorldGen;
using TermRTS.Io;

namespace TermRTS.Examples.Greenery;

// TODO: Make world generation parameters changeable in-game.
// TODO: --> Create concept for a debug mode.

public class Greenery : IRunnableExample
{
    private const int Seed = 3;
    private const int VoronoiCellCount = 175;
    private const int PlateCount = 28;

    // private readonly ILog _log;
    private CommandRunner _commandRunner;

    #region IRunnableExample Members

    public void Run()
    {
        var previousTitle = "Powershell";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            previousTitle = Console.Title;
            Console.Title = "TermRTS - Greenery";
        }


        // Set up engine
        var core = new Core();

        var worldGen =
            new CylinderWorld(
                WorldMath.WorldWidth,
                WorldMath.WorldHeight,
                0.40f,
                Seed,
                VoronoiCellCount,
                PlateCount,
                new ElevationParameters(),
                new CoastalParameters(),
                new VolcanicParameters(),
                new ErosionParameters(),
                new ClimateParameters(),
                new RiverParameters());
        var worldData = worldGen.Generate();
        core.AddNewComponents(worldData.ElevationChunk);
        core.AddNewComponents(worldData.SurfaceFeatureChunk);
        core.AddNewComponents(worldData.TemperatureChunk);
        core.AddNewComponents(worldData.HumidityChunk);
        core.AddNewComponents(worldData.TemperatureAmplitudeChunk);
        core.AddNewComponents(worldData.BiomeChunk);
        core.AddNewComponents(worldData.RiverChunk);
        core.AddNewComponents(worldData.PackedData);

        var fovSystem = new FovSystem();
        core.AddNewComponents(fovSystem.InitializeFovChunks());

        var droneEntity = new Entity();
        var droneComponent = new DroneComponent(droneEntity.Id, new Vector2(1, 1));
        core.AddEntity(droneEntity);
        core.AddComponent(droneComponent);

        var pathFindingSystem =
            new PathFindingSystem(WorldMath.WorldWidth, WorldMath.WorldHeight);
        core.AddSimSystem(pathFindingSystem);
        core.AddSimSystem(fovSystem);

        var scheduler = new Scheduler(core);
        var renderer =
            new Renderer(scheduler.FutureEvents, WorldMath.WorldWidth, WorldMath.WorldHeight,
                new UiThemes());
        core.Renderer = renderer;
        scheduler.AddEventSink(renderer, typeof(Profile));

        // Listen to commands
        _commandRunner = new CommandRunner(scheduler.FutureEvents);
        scheduler.AddEventSink(renderer, typeof(MapRenderMode)); // render option events
        scheduler.AddEventSink(_commandRunner, typeof(Event.Command));
        scheduler.AddEventSink(pathFindingSystem, typeof(Move));

        // Init input
        var input = new ConsoleInput(scheduler.FutureEvents, ConsoleKey.Escape);
        scheduler.AddEventSink(input, typeof(Shutdown));
        scheduler.AddEventSink(renderer, typeof(ConsoleKeyInfo));
        scheduler.AddEventSink(renderer.LogArea, typeof(SystemLog));
        input.Run();

        var simulation = new Simulation(scheduler);
        simulation.EnableSerialization();
        simulation.IsSystemLogEnabled = true;

        // Graceful shutdown on canceling via CTRL+C.
        Console.CancelKeyPress += delegate (object? _, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            scheduler.FutureEvents.EnqueueEvent(ScheduledEvent.From(new Shutdown()));
            Console.Clear();
            Console.WriteLine("Simulation was shut down. Press a key to exit the program:");
        };

        // Run it
        simulation.Run();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) Console.Title = previousTitle;
    }

    #endregion
}