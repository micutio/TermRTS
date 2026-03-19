using System.Runtime.InteropServices;
using TermRTS.Event;
using TermRTS.Examples.Greenery;
using TermRTS.Examples.Hillshade.System;
using TermRTS.Io;

namespace TermRTS.Examples.Hillshade;

public class Hillshade : IRunnableExample
{
    private const int WorldWidth = 300;
    private const int WorldHeight = 250;

    public void Run()
    {
        var previousTitle = "Powershell";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            previousTitle = Console.Title;
            Console.Title = "TermRTS - Hillshade";
        }

        const int seed = 0;
        var core = new Core();

        var worldGen = new VoronoiWorld(50, seed);
        var worldEntity = new EntityBase();
        var worldComponent = new WorldComponent(
            worldEntity.Id,
            WorldWidth,
            WorldHeight,
            worldGen.Generate(WorldWidth, WorldHeight));
        core.AddEntity(worldEntity);
        core.AddComponent(worldComponent);

        var timeEntity = new EntityBase();
        var dayLengthMs = (ulong)(24 * 60 * 60 * 100); // 24 "game" minutes per full day
        var timeOfDayComponent = new TimeOfDayComponent(timeEntity.Id, 0, dayLengthMs);
        core.AddEntity(timeEntity);
        core.AddComponent(timeOfDayComponent);

        core.AddSimSystem(new TimeOfDaySystem());

        var scheduler = new Scheduler(core);
        var renderer = new Renderer(scheduler.EventQueue, WorldWidth, WorldHeight);
        core.Renderer = renderer;

        var input = new ConsoleInput(scheduler.EventQueue, ConsoleKey.Escape);
        scheduler.AddEventSink(input, typeof(Shutdown));
        scheduler.AddEventSink(renderer, typeof(ConsoleKeyInfo));
        input.Run();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            scheduler.EventQueue.EnqueueEvent(ScheduledEvent.From(new Shutdown()));
            Console.Clear();
            Console.WriteLine("Simulation was shut down. Press a key to exit the program:");
        };

        new Simulation(scheduler).Run();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Console.Title = previousTitle;
    }
}
