using System.Numerics;
using System.Runtime.InteropServices;
using TermRTS.Event;
using TermRTS.Examples.Greenery.Command;
using TermRTS.Examples.Greenery.Ecs.Component;
using TermRTS.Examples.Greenery.Serialization;
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
    private const int VoronoiCellCount = 500;
    private const int PlateCount = 20;

    // private readonly ILog _log;
    private CommandRunner? _commandRunner;

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

        WorldPackedChunk[] worldData;
        {
            worldData =
                new CylinderWorld(
                    WorldMath.WorldWidth,
                    WorldMath.WorldHeight,
                    0.35f,
                    Seed,
                    VoronoiCellCount,
                    PlateCount,
                    new ElevationParameters(),
                    new VolcanicParameters(),
                    new ErosionParameters(),
                    new ClimateParameters(),
                    new RiverParameters()).Generate();
        }

        // 1. Tell the GC to compact the Large Object Heap on the next sweep
        // System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
        // 2. Force a full, blocking Generation 2 collection
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        // 3. Wait for any straggling finalizers
        GC.WaitForPendingFinalizers();

        core.AddNewComponents(worldData);

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

        // Register component serializers/deserializers for Greenery example
        // This uses source-generated JsonTypeInfo from GreeneryJsonContext
        var reg = TermRTS.ComponentRegistry.Instance;

        reg.Register<DroneComponent, DroneData>(GreeneryJsonContext.Default.DroneData,
            comp => new DroneData { X = comp.Position.X, Y = comp.Position.Y },
            (eid, d) => new DroneComponent(eid, new Vector2(d.X, d.Y)));

        reg.Register<FovChunk, FovChunkData>(GreeneryJsonContext.Default.FovChunkData,
            comp => new FovChunkData { Cx = comp.Cx, Cy = comp.Cy, FovField = comp.FovField.ToArray() },
            (eid, d) => new FovChunk(eid, d.Cx, d.Cy, d.FovField.AsMemory()));

        reg.Register<WorldElevationChunk, WorldElevationChunkData>(GreeneryJsonContext.Default.WorldElevationChunkData,
            comp => new WorldElevationChunkData { Cx = comp.Cx, Cy = comp.Cy, Elevation = comp.Elevation },
            (eid, d) => new WorldElevationChunk(eid, d.Cx, d.Cy, d.Elevation));

        reg.Register<WorldSurfaceFeatureChunk, WorldSurfaceFeatureChunkData>(GreeneryJsonContext.Default.WorldSurfaceFeatureChunkData,
            comp => new WorldSurfaceFeatureChunkData { Cx = comp.Cx, Cy = comp.Cy, SurfaceFeature = comp.SurfaceFeature },
            (eid, d) => new WorldSurfaceFeatureChunk(eid, d.Cx, d.Cy, d.SurfaceFeature));

        reg.Register<WorldTemperatureChunk, WorldTemperatureChunkData>(GreeneryJsonContext.Default.WorldTemperatureChunkData,
            comp => new WorldTemperatureChunkData { Cx = comp.Cx, Cy = comp.Cy, Temperature = comp.Temperature },
            (eid, d) => new WorldTemperatureChunk(eid, d.Cx, d.Cy, d.Temperature));

        reg.Register<WorldTemperatureAmplitudeChunk, WorldTemperatureAmplitudeChunkData>(GreeneryJsonContext.Default.WorldTemperatureAmplitudeChunkData,
            comp => new WorldTemperatureAmplitudeChunkData { Cx = comp.Cx, Cy = comp.Cy, TemperatureAmplitude = comp.TemperatureAmplitude },
            (eid, d) => new WorldTemperatureAmplitudeChunk(eid, d.Cx, d.Cy, d.TemperatureAmplitude));

        reg.Register<WorldHumidityChunk, WorldHumidityChunkData>(GreeneryJsonContext.Default.WorldHumidityChunkData,
            comp => new WorldHumidityChunkData { Cx = comp.Cx, Cy = comp.Cy, Humidity = comp.Humidity },
            (eid, d) => new WorldHumidityChunk(eid, d.Cx, d.Cy, d.Humidity));

        reg.Register<WorldBiomeChunk, WorldBiomeChunkData>(GreeneryJsonContext.Default.WorldBiomeChunkData,
            comp => new WorldBiomeChunkData { Cx = comp.Cx, Cy = comp.Cy, Biome = comp.Biome },
            (eid, d) => new WorldBiomeChunk(eid, d.Cx, d.Cy, d.Biome));

        reg.Register<WorldRiverChunk, WorldRiverChunkData>(GreeneryJsonContext.Default.WorldRiverChunkData,
            comp => new WorldRiverChunkData { Cx = comp.Cx, Cy = comp.Cy, River = comp.River },
            (eid, d) => new WorldRiverChunk(eid, d.Cx, d.Cy, d.River));

        reg.Register<WorldPackedChunk, WorldPackedChunkData>(GreeneryJsonContext.Default.WorldPackedChunkData,
            comp => new WorldPackedChunkData { Cx = comp.Cx, Cy = comp.Cy, PackedTiles = comp.PackedTiles },
            (eid, d) => new WorldPackedChunk(eid, d.Cx, d.Cy, d.PackedTiles));

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