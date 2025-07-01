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

public class EngineTestParallel : TheoryData<Core>
{
    public EngineTestParallel()
    {
        Add(
            new Core(new NullRenderer())
            {
                IsParallelized = true
            });
    }
}

public class EngineTestSerial : TheoryData<Core>
{
    public EngineTestSerial()
    {
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

    public void ProcessComponents(ulong timeStepSize, in IReadonlyStorage storage)
    {
        Interlocked.Decrement(ref _remainingTicks);
        Console.WriteLine($"[WatcherSystem] remaining ticks: {_remainingTicks}");

        if (_remainingTicks != 0) return;

        // _eventChannel.Writer.TryWrite(ScheduledEvent.From(new Shutdown()));
        var shutdownEvent = ScheduledEvent.From(new Shutdown());
        var task = _eventChannel.Writer.WriteAsync(shutdownEvent);
        task.AsTask().Wait();
        Console.WriteLine("No more ticks left. Send SHUTDOWN to scheduler");
    }

    #endregion
}

public class EngineTest
{
    [Theory]
    [ClassData(typeof(EngineTestParallel))]
    [ClassData(typeof(EngineTestSerial))]
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

    /// Do not run this test parallelized since this fails on Windows and Linux for some reason
    /// in GitHub Actions. Seems to work fine on desktop, but can be reproduced by debugging
    /// and pausing the run for a second.
    // TODO: Fix parallelized run.
    [Theory]
    [ClassData(typeof(EngineTestSerial))]
    public void TestSchedulerSetup(Core core)
    {
        // Setup Scheduler
        var watcherSystem = new WatcherSystem(12);
        var scheduler = new Scheduler(core);
        scheduler.AddEventSources(watcherSystem.EventOutput);
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
    [ClassData(typeof(EngineTestParallel))]
    [ClassData(typeof(EngineTestSerial))]
    public void TestScheduledEvent(Core core)
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
    [ClassData(typeof(EngineTestParallel))]
    [ClassData(typeof(EngineTestSerial))]
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