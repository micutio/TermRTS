using TermRTS.Event;
using TermRTS.Storage;

namespace TermRTS.Examples.Minimal;

internal class NullRenderer : IRenderer
{
    #region IRenderer Members

    public void RenderComponents(in IReadonlyStorage components, double timeStepSizeMs,
        double howFarIntoNextFramePercent)
    {
        // Console.WriteLine($"Rendering null-entity at {howFarIntoNextFrameMs} ms into next frame.");
    }

    public void FinalizeRender()
    {
    }

    public void Shutdown()
    {
    }

    #endregion
}

internal class WatcherSystem : ISimSystem
{
    private int _remainingTicks;

    public WatcherSystem(int remainingTicks)
    {
        _remainingTicks = remainingTicks;
    }

    #region ISimSystem Members

    public void ProcessComponents(
        ulong timeStepSizeMs,
        in IReadonlyStorage storage,
        in List<ScheduledEvent> emittedEvents)
    {
        _remainingTicks -= 1;

        if (_remainingTicks == 0)
            emittedEvents.Add(ScheduledEvent.From(new Shutdown()));

        if (_remainingTicks % 60 == 0)
            emittedEvents.Add(ScheduledEvent.From(new Profile(), 60UL));
    }

    #endregion
}

internal class MinimalApp : IRunnableExample
{
    #region IRunnableExample Members

    public void Run()
    {
        var core = new Core
        {
            Renderer = new NullRenderer()
        };
        var scheduler = new Scheduler(core);
        var watcherSystem = new WatcherSystem(12);
        core.AddSimSystem(watcherSystem);
        core.AddEntity(new Entity());


        // Alternative solution: enqueue an event which fires after a given time
        // scheduler.EnqueueEvent((new PlainEvent(EventType.Profile), 12 * 16));
        var simulation = new Simulation(scheduler);

        // Run it
        simulation.Run();

        // It should terminate after 12 ticks of 16ms simulated time each.
    }

    #endregion
}