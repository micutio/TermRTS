using System.Threading.Channels;
using TermRTS.Event;

namespace TermRTS.Test;

public class NullRenderer : IRenderer
{
    #region IRenderer Members

    public void RenderComponents(in IStorage storage, double timeStepSizeMs,
        double howFarIntoNextFramePercent)
    {
    }

    public void Shutdown()
    {
    }

    void IRenderer.FinalizeRender()
    {
    }

    #endregion
}

public class EngineTestTheoryData : TheoryData<Core>
{
    public EngineTestTheoryData()
    {
        Add(new Core(new NullRenderer()));
    }
}

public class NullEntity : EntityBase
{
}

public class WatcherSystem : ISimSystem
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

    public void ProcessComponents(ulong timeStepSize, in IStorage storage)
    {
        _remainingTicks -= 1;

        if (_remainingTicks == 0)
            _eventChannel.Writer.TryWrite(ScheduledEvent.From(new Shutdown()));
    }

    #endregion
}

public class EngineTest
{
    [Theory]
    [ClassData(typeof(EngineTestTheoryData))]
    public void TestSetup(Core core)
    {
        Assert.True(core.IsRunning());
        core.Tick(16L);
        core.Tick(16L);
        core.Tick(16L);
        core.Tick(16L);
        core.ProcessEvent(new Event<Shutdown>());
        Assert.False(core.IsRunning());
    }

    [Theory]
    [ClassData(typeof(EngineTestTheoryData))]
    public void TestSchedulerSetup(Core core)
    {
        // Setup Scheduler
        var watcherSystem = new WatcherSystem(12);
        var scheduler = new Scheduler(core);
        scheduler.AddEventSources(watcherSystem.EventOutput);
        scheduler.AddEventSink(core, typeof(Shutdown));
        core.AddSimSystem(watcherSystem);
        core.AddEntity(new NullEntity());

        // Run it
        var simulation = new Simulation(scheduler);
        simulation.Run();

        // It should terminate after 12 ticks of 16ms simulated time each.
        const ulong finalTime = 12 * 16;
        Assert.Equal(finalTime, scheduler.TimeMs);
    }

    [Theory]
    [ClassData(typeof(EngineTestTheoryData))]
    public void TestScheduledEvent(Core core)
    {
        // Setup Scheduler
        var scheduler = new Scheduler(core);
        scheduler.AddEventSink(core, typeof(Shutdown));
        scheduler.EnqueueEvent(ScheduledEvent.From(new Shutdown(), 12 * 16));

        // Run it
        var simulation = new Simulation(scheduler);
        simulation.Run();

        // It should terminate after 12 ticks of 16ms simulated time each.
        const ulong finalTime = 12 * 16;
        Assert.Equal(finalTime, scheduler.TimeMs);
    }

    [Theory]
    [ClassData(typeof(EngineTestTheoryData))]
    public void TestSerialization(Core core)
    {
        // Setup Scheduler
        var watcherSystem = new WatcherSystem(12);
        var scheduler = new Scheduler(core);
        scheduler.AddEventSources(watcherSystem.EventOutput);
        scheduler.AddEventSink(core, typeof(Shutdown));
        core.AddSimSystem(watcherSystem);
        core.AddEntity(new NullEntity());
        // Setup Simulation and Persistence
        var sim = new Simulation(scheduler);
        var persistence = new Persistence();
        var serializationError1 =
            persistence.SerializeSimulationStateToJson(ref scheduler, out var expectedJson);

        Assert.Null(serializationError1);
        Assert.NotNull(expectedJson);

        var deserializationError =
            persistence.LoadSimulationStateFromJson(ref scheduler, expectedJson);

        Assert.Null(deserializationError);

        var serializationError2 =
            persistence.SerializeSimulationStateToJson(ref scheduler, out var actualJson);

        Assert.Null(serializationError2);
        Assert.NotNull(actualJson);
        Assert.Equal(expectedJson, actualJson);
    }
}