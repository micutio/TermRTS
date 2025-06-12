using System.Threading.Channels;
using TermRTS.Event;

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
    private readonly Channel<ScheduledEvent> _eventChannel;
    public readonly ChannelReader<ScheduledEvent> EventOutput;
    private int _remainingTicks;

    public WatcherSystem(int remainingTicks)
    {
        _remainingTicks = remainingTicks;
        _eventChannel = Channel.CreateUnbounded<ScheduledEvent>();
        EventOutput = _eventChannel.Reader;
    }

    #region ISimSystem Members

    public void ProcessComponents(ulong timeStepSizeMs, in IReadonlyStorage storage)
    {
        _remainingTicks -= 1;
        Console.WriteLine($"[WatcherSystem] remaining ticks: {_remainingTicks}");

        if (_remainingTicks == 0)
            _eventChannel.Writer.TryWrite(ScheduledEvent.From(new Shutdown()));

        if (_remainingTicks % 60 == 0)
            _eventChannel.Writer.TryWrite(ScheduledEvent.From(new Profile(), 60UL));
    }

    #endregion
}

internal class MinimalApp : IRunnableExample
{
    #region IRunnableExample Members

    public void Run()
    {
        var core = new Core(new NullRenderer());
        var watcherSystem = new WatcherSystem(12);
        core.AddSimSystem(watcherSystem);
        core.AddEntity(new EntityBase());

        var scheduler = new Scheduler(core);
        scheduler.AddEventSources(watcherSystem.EventOutput);
        scheduler.AddEventSink(core, typeof(Shutdown));

        // Alternative solution: enqueue an event which fires after a given time
        // scheduler.EnqueueEvent((new PlainEvent(EventType.Profile), 12 * 16));
        var simulation = new Simulation(scheduler);

        // Run it
        simulation.Run();

        // It should terminate after 12 ticks of 16ms simulated time each.
    }

    #endregion
}