using System.Threading.Channels;

namespace TermRTS.Examples.Testing;

public class NullWorld : TermRTS.IWorld
{
    public void ApplyChange()
    {
        // Console.WriteLine("World changed.");
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
        // Console.WriteLine($"Rendering null-entity at {howFarIntoNextFrameMs} ms into next frame.");
    }

    public void RenderWorld(NullWorld world, double howFarIntoNextFrameMs)
    {
        // Console.WriteLine($"Rendering null-world at {howFarIntoNextFrameMs} ms into next frame.");
    }
}

public class NullEntity : EntityBase<EmptyComponentType>
{

}

public class WatcherSystem : System<NullWorld, EmptyComponentType>
{
    private int _remainingTicks;
    private readonly Channel<(IEvent, UInt64)> _eventChannel;
    public readonly ChannelReader<(IEvent, UInt64)> EventOutput;

    public WatcherSystem(int remainingTicks)
    {
        _remainingTicks = remainingTicks;
        _eventChannel = Channel.CreateUnbounded<(IEvent, UInt64)>();
        EventOutput = _eventChannel.Reader;
    }

    public override Dictionary<EmptyComponentType, IComponent>? ProcessComponents(
            UInt64 timeStepSizeMs,
            EntityBase<EmptyComponentType> thisEntityComponents,
            List<EntityBase<EmptyComponentType>> otherEntityComponents,
            ref NullWorld world)
    {
        _remainingTicks -= 1;
        // Console.WriteLine($"[WatcherSystem] remaining ticks: {_remainingTicks}");

        if (_remainingTicks == 0)
        {
            _eventChannel.Writer.TryWrite((new PlainEvent(EventType.Shutdown), 0));
        }

        if (_remainingTicks % 60 == 0)
        {
            _eventChannel.Writer.TryWrite((new PlainEvent(EventType.Profile), 60));
        }

        return new Dictionary<EmptyComponentType, IComponent>();
    }

}

public static class DebugProgram
{
    public static void Main()
    {
        var core = new Core<NullWorld, EmptyComponentType>(new NullWorld(), new NullRenderer());
        // Setup Scheduler
        var watcherSystem = new WatcherSystem(remainingTicks: 10);
        var scheduler = new Scheduler(16, 16, core);
        scheduler.AddEventSources(watcherSystem.EventOutput);
        scheduler.AddEventSink(core, EventType.Shutdown);
        // scheduler.QueueEvent((new PlainEvent(EventType.Profile), 60));
        core.AddGameSystem(watcherSystem);
        core.AddEntity(new NullEntity());

        // Run it
        scheduler.GameLoop();

        // It should terminate after 12 ticks of 16ms simulated time each.
    }
}
