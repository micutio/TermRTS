using System.Threading.Channels;

namespace TermRTS.Test;

public class NullWorld : IWorld
{
    public void ApplyChange()
    {
        Console.WriteLine("World changed.");
    }
}

public class NullRenderer : IRenderer<NullWorld>
{
    public void RenderEntity(
        Dictionary<Type, IComponent> entity,
        double howFarIntoNextFrameMs)
    {
        Console.WriteLine($"Rendering null-entity at {howFarIntoNextFrameMs} ms into next frame.");
    }

    public void RenderWorld(NullWorld world, double howFarIntoNextFrameMs, double timeStepSizeMs)
    {
        Console.WriteLine($"Rendering null-world at {howFarIntoNextFrameMs} ms into next frame.");
    }

    public void Shutdown()
    {
    }

    void IRenderer<NullWorld>.FinalizeRender()
    {
    }
}

public class EngineTestTheoryData : TheoryData<Core<NullWorld>>
{
    public EngineTestTheoryData()
    {
        Add(new Core<NullWorld>(new NullWorld(), new NullRenderer()));
    }
}

public class NullEntity : EntityBase
{
}

public class WatcherSystem : System<NullWorld>
{
    private readonly Channel<(IEvent, ulong)> _eventChannel;
    private int _remainingTicks;
    public ChannelReader<(IEvent, ulong)> EventOutput;

    public WatcherSystem(int remainingTicks)
    {
        _remainingTicks = remainingTicks;
        _eventChannel = Channel.CreateUnbounded<(IEvent, ulong)>();
        EventOutput = _eventChannel.Reader;
    }

    public override Dictionary<Type, IComponent>? ProcessComponents(
        ulong timeStepSize,
        EntityBase thisEntityComponents,
        IEnumerable<EntityBase> otherEntityComponents,
        ref NullWorld world)
    {
        _remainingTicks -= 1;

        if (_remainingTicks == 1)
            _eventChannel.Writer.TryWrite((new PlainEvent(EventType.Shutdown), 0));

        return null;
    }
}

public class EngineTest
{
    [Theory]
    [ClassData(typeof(EngineTestTheoryData))]
    public void TestSetup(Core<NullWorld> core)
    {
        Assert.True(core.IsRunning());
        core.Tick(16L);
        core.Tick(16L);
        core.Tick(16L);
        core.Tick(16L);
        core.ProcessEvent(new PlainEvent(EventType.Shutdown));
        Assert.False(core.IsRunning());
    }

    [Theory]
    [ClassData(typeof(EngineTestTheoryData))]
    public void TestSchedulerSetup(Core<NullWorld> core)
    {
        // Setup Scheduler
        var watcherSystem = new WatcherSystem(12);
        var scheduler = new Scheduler(16, 16, core);
        scheduler.AddEventSources(watcherSystem.EventOutput);
        scheduler.AddEventSink(core, EventType.Shutdown);
        core.AddGameSystem(watcherSystem);
        core.AddEntity(new NullEntity());

        // Run it
        scheduler.SimulationLoop();

        // It should terminate after 12 ticks of 16ms simulated time each.
        ulong finalTime = 12 * 16;
        Assert.Equal(finalTime, scheduler.TimeMs);
    }

    [Theory]
    [ClassData(typeof(EngineTestTheoryData))]
    public void TestScheduledEvent(Core<NullWorld> core)
    {
        // Setup Scheduler
        var scheduler = new Scheduler(16, 16, core);
        scheduler.AddEventSink(core, EventType.Shutdown);
        scheduler.EnqueueEvent((new PlainEvent(EventType.Shutdown), 12 * 16));

        // Run it
        scheduler.SimulationLoop();

        // It should terminate after 12 ticks of 16ms simulated time each.
        ulong finalTime = 12 * 16;
        Assert.Equal(finalTime, scheduler.TimeMs);
    }
}