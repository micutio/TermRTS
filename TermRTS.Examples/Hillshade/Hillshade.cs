using TermRTS.Event;
using TermRTS.Examples.Hillshade.System;
using TermRTS.Io;
using TermRTS.Shared.Harness;
using TermRTS.Shared.World;

namespace TermRTS.Examples.Hillshade;

public class Hillshade : IRunnableExample
{
    public void Run()
    {
        var previousTitle = ConsoleTitleHelper.SaveAndSet("TermRTS - Hillshade");

        const int seed = 0;
        var core = new Core();

        var worldGen = new VoronoiWorld(50, seed);
        var worldEntity = new EntityBase();
        var worldComponent = new WorldComponent(
            worldEntity.Id,
            DefaultWorldDimensions.Width,
            DefaultWorldDimensions.Height,
            worldGen.Generate(DefaultWorldDimensions.Width, DefaultWorldDimensions.Height));
        core.AddEntity(worldEntity);
        core.AddComponent(worldComponent);

        var timeEntity = new EntityBase();
        var dayLengthMs = (ulong)(24 * 60 * 60 * 100); // 24 "game" minutes per full day
        var timeOfDayComponent = new TimeOfDayComponent(timeEntity.Id, 0, dayLengthMs);
        core.AddEntity(timeEntity);
        core.AddComponent(timeOfDayComponent);

        core.AddSimSystem(new TimeOfDaySystem());

        var scheduler = new Scheduler(core);
        var renderer = new Renderer(scheduler.EventQueue, DefaultWorldDimensions.Width,
            DefaultWorldDimensions.Height);
        core.Renderer = renderer;

        var input = new ConsoleInput(scheduler.EventQueue, ConsoleKey.Escape);
        scheduler.AddEventSink(input, typeof(Shutdown));
        scheduler.AddEventSink(renderer, typeof(ConsoleKeyInfo));
        input.Run();

        GracefulShutdown.RegisterCancelHandler(scheduler.EventQueue);

        new Simulation(scheduler).Run();

        ConsoleTitleHelper.Restore(previousTitle);
    }
}
