using System.Threading.Channels;
using TermRTS.Event;

namespace TermRTS.Test;

public class NullRenderer : IRenderer
{
    #region IRenderer Members

    public void RenderComponents(in IReadonlyStorage storage, double timeStepSizeMs,
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

public class TestCoresSequentialAndParallel : TheoryData<Core>
{
    public TestCoresSequentialAndParallel()
    {
        Add(
            new Core(new NullRenderer())
            {
                IsParallelized = true
            });
        Add(
            new Core(new NullRenderer())
            {
                IsParallelized = false
            });
    }
}

public class NullEntity : EntityBase
{
}

public class TerminatorSystem : ISimSystem
{
    private readonly Channel<ScheduledEvent> _eventChannel;
    public readonly ChannelReader<ScheduledEvent> EventOutput;
    private int _remainingTicks;

    public TerminatorSystem(int remainingTicks)
    {
        _remainingTicks = remainingTicks;
        _eventChannel = Channel.CreateBounded<ScheduledEvent>(1);
        EventOutput = _eventChannel.Reader;
    }

    #region ISimSystem Members

    public void ProcessComponents(ulong timeStepSize, in IReadonlyStorage storage)
    {
        Interlocked.Decrement(ref _remainingTicks);

        if (_remainingTicks != 0) return;

        var isSuccess = _eventChannel.Writer.TryWrite(ScheduledEvent.From(new Shutdown()));
        Assert.True(isSuccess);
    }

    #endregion
}

/// <summary>
/// A system that wastes a certain amount of time to simulate work.
/// </summary>
public class BusySystem(double workTimeMs) : ISimSystem
{
    private readonly TimeSpan _workTime = TimeSpan.FromMilliseconds(workTimeMs);

    public void ProcessComponents(ulong timeStepSizeMs, in IReadonlyStorage storage)
    {
        var start = DateTime.Now;
        while (DateTime.Now - start < _workTime) ;
    }
}

public class EngineTest
{
    [Theory]
    [ClassData(typeof(TestCoresSequentialAndParallel))]
    public void TestCoreShutdown(Core core)
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
    [ClassData(typeof(TestCoresSequentialAndParallel))]
    public void TestSchedulerShutdown(Core core)
    {
        // Setup Scheduler
        var watcherSystem = new TerminatorSystem(12);
        var scheduler = new Scheduler(core);
        scheduler.AddEventSources(watcherSystem.EventOutput);

        core.AddSimSystem(watcherSystem);
        core.AddSimSystem(new BusySystem(2.0d));
        core.AddSimSystem(new BusySystem(1.0d));
        core.AddSimSystem(new BusySystem(3.0d));
        core.AddSimSystem(new BusySystem(4.0d));

        core.AddEntity(new NullEntity());

        // Run it
        var simulation = new Simulation(scheduler);
        simulation.Run();

        // It should terminate after 12 ticks of 16ms simulated time each.
        const ulong finalTime = 12 * 16;
        Assert.Equal(finalTime, scheduler.TimeMs);
    }

    [Theory]
    [ClassData(typeof(TestCoresSequentialAndParallel))]
    public void TestBusyCore(Core core)
    {
        // Setup Scheduler
        var watcherSystem = new TerminatorSystem(12);
        var scheduler = new Scheduler(core);
        scheduler.AddEventSources(watcherSystem.EventOutput);

        core.AddSimSystem(watcherSystem);
        core.AddSimSystem(new BusySystem(2.0d));
        core.AddSimSystem(new BusySystem(1.0d));
        core.AddSimSystem(new BusySystem(3.0d));
        core.AddSimSystem(new BusySystem(4.0d));

        core.AddEntity(new NullEntity());

        // Run it
        var simulation = new Simulation(scheduler);
        simulation.Run();

        // It should terminate after 12 ticks of 16ms simulated time each.
        const ulong finalTime = 12 * 16;
        Assert.Equal(finalTime, scheduler.TimeMs);
    }

    [Theory]
    [ClassData(typeof(TestCoresSequentialAndParallel))]
    public void TestDelayedEventScheduling(Core core)
    {
        // Set up Scheduler
        var scheduler = new Scheduler(core);
        scheduler.EnqueueEvent(ScheduledEvent.From(new Shutdown(), 12 * 16));

        // Run it
        var simulation = new Simulation(scheduler);
        simulation.Run();

        // It should terminate after 12 ticks of 16ms simulated time each.
        const ulong finalTime = 12 * 16;
        Assert.Equal(finalTime, scheduler.TimeMs);
    }

    [Theory]
    [ClassData(typeof(TestCoresSequentialAndParallel))]
    public void TestSerialization(Core core)
    {
        // Setup Scheduler
        var watcherSystem = new TerminatorSystem(12);
        var scheduler = new Scheduler(core);
        scheduler.AddEventSources(watcherSystem.EventOutput);
        scheduler.AddEventSink(core, typeof(Shutdown));
        core.AddSimSystem(watcherSystem);
        core.AddEntity(new NullEntity());
        // Setup Simulation and Persistence
        var persistence = new Persistence();
        var serializationSuccess =
            persistence.PutSimStateToJson(ref scheduler, out var expectedJson, out _);

        Assert.True(serializationSuccess);
        Assert.NotNull(expectedJson);

        var deserializationSuccess =
            persistence.GetSimStateFromJson(ref scheduler, expectedJson, out _);

        Assert.True(deserializationSuccess);

        var serializationSuccess2 =
            persistence.PutSimStateToJson(ref scheduler, out var actualJson, out _);

        Assert.True(serializationSuccess2);
        Assert.NotNull(actualJson);
        Assert.Equal(expectedJson, actualJson);
    }
}