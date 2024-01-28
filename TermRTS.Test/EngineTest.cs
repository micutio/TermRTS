
using System.Threading.Channels;

namespace TermRTS.Test;

public class NullWorld : IWorld
{
    public void ApplyChange()
    {
        Console.WriteLine("World changed.");
    }
}

public enum EmptyComponentType
{
    Empty,
}

public class NullRenderer : IRenderer<NullWorld, EmptyComponentType>
{
    public void RenderEntity(
            Dictionary<EmptyComponentType, IComponent> entity,
            double howFarIntoNextFrameMs)
    {
        Console.WriteLine($"Rendering null-entity at {howFarIntoNextFrameMs} ms into next frame.");
    }

    public void RenderWorld(NullWorld world, double howFarIntoNextFrameMs)
    {
        Console.WriteLine($"Rendering null-world at {howFarIntoNextFrameMs} ms into next frame.");
    }
}

public class EngineTestTheoryData : TheoryData<Core<NullWorld, EmptyComponentType>>
{
    public EngineTestTheoryData()
    {
        Add(new Core<NullWorld, EmptyComponentType>(new NullWorld(), new NullRenderer()));
    }
}

public class NullEntity : EntityBase<EmptyComponentType> { }

public class WatcherSystem : System<NullWorld, EmptyComponentType>
{
    private int _remainingTicks;
    private Channel<(IEvent, UInt64)> _eventChannel;
    public ChannelReader<(IEvent, UInt64)> EventOutput;

    public WatcherSystem(int remainingTicks)
    {
        _remainingTicks = remainingTicks;
        _eventChannel = Channel.CreateUnbounded<(IEvent, UInt64)>();
        EventOutput = _eventChannel.Reader;
    }

    public override Dictionary<EmptyComponentType, IComponent>? ProcessComponents(
            UInt64 timeStepSize,
            EntityBase<EmptyComponentType> thisEntityComponents,
            List<EntityBase<EmptyComponentType>> otherEntityComponents,
            ref NullWorld world)
    {
        _remainingTicks -= 1;

        if (_remainingTicks == 0)
        {
            _eventChannel.Writer.TryWrite((new PlainEvent(EventType.Shutdown), 0));
        }

        return new Dictionary<EmptyComponentType, IComponent>();
    }

}

public class EngineTest
{
    [Theory]
    [ClassData(typeof(EngineTestTheoryData))]
    public void TestSetup(Core<NullWorld, EmptyComponentType> core)
    {
        Assert.True(core.IsRunning());
        core.Tick(16L);
        core.Tick(16L);
        core.Tick(16L);
        core.Tick(16L);
        core.Shutdown();
        Assert.False(core.IsRunning());
    }

    [Theory]
    [ClassData(typeof(EngineTestTheoryData))]
    public void TestSchedulerSetup(Core<NullWorld, EmptyComponentType> core)
    {
        // Setup Scheduler
        var watcherSystem = new WatcherSystem(remainingTicks: 12);
        var scheduler = new Scheduler(16, 16, core);
        scheduler.AddEventSources(watcherSystem.EventOutput);
        scheduler.AddEventSink(core, EventType.Shutdown);
        core.AddGameSystem(watcherSystem);
        core.AddEntity(new NullEntity());

        // Run it
        scheduler.SimulationLoop();

        // It should terminate after 12 ticks of 16ms simulated time each.
        UInt64 finalTime = 12 * 16;
        Assert.Equal(finalTime, scheduler.TimeMs);
    }

    [Theory]
    [ClassData(typeof(EngineTestTheoryData))]
    public void TestScheduledEvent(Core<NullWorld, EmptyComponentType> core)
    {
        // Setup Scheduler
        var scheduler = new Scheduler(16, 16, core);
        scheduler.AddEventSink(core, EventType.Shutdown);
        scheduler.EnqueueEvent((new PlainEvent(EventType.Shutdown), 12 * 16));

        // Run it
        scheduler.SimulationLoop();

        // It should terminate after 12 ticks of 16ms simulated time each.
        UInt64 finalTime = 12 * 16;
        Assert.Equal(finalTime, scheduler.TimeMs);
    }
}
