using System.Threading.Channels;

namespace TermRTS.Examples.Testing;

internal class NullWorld : IWorld
{
    public void ApplyChange()
    {
        // Console.WriteLine("World changed.");
    }
}

internal enum EmptyComponentType
{
    Empty
}

internal class NullRenderer : IRenderer<NullWorld, EmptyComponentType>
{
    public void RenderEntity(
        Dictionary<EmptyComponentType, IComponent> entity,
        double howFarIntoNextFrameMs)
    {
        // Console.WriteLine($"Rendering null-entity at {howFarIntoNextFrameMs} ms into next frame.");
    }

    public void RenderWorld(NullWorld world, double howFarIntoNextFrameMs, double timeStepSizeMs)
    {
        // Console.WriteLine($"Rendering null-world at {howFarIntoNextFrameMs} ms into next frame.");
    }

    public void FinalizeRender()
    {
    }

    public void Shutdown()
    {
    }
}

internal class NullEntity : EntityBase<EmptyComponentType>
{
}

internal class WatcherSystem : System<NullWorld, EmptyComponentType>
{
    private readonly Channel<(IEvent, ulong)> _eventChannel;
    public readonly ChannelReader<(IEvent, ulong)> EventOutput;
    private int _remainingTicks;

    public WatcherSystem(int remainingTicks)
    {
        _remainingTicks = remainingTicks;
        _eventChannel = Channel.CreateUnbounded<(IEvent, ulong)>();
        EventOutput = _eventChannel.Reader;
    }

    public override Dictionary<EmptyComponentType, IComponent>? ProcessComponents(
        ulong timeStepSizeMs,
        EntityBase<EmptyComponentType> thisEntityComponents,
        IEnumerable<EntityBase<EmptyComponentType>> otherEntityComponents,
        ref NullWorld world)
    {
        _remainingTicks -= 1;
        // Console.WriteLine($"[WatcherSystem] remaining ticks: {_remainingTicks}");

        if (_remainingTicks == 0)
            _eventChannel.Writer.TryWrite((new PlainEvent(EventType.Shutdown), 0));

        if (_remainingTicks % 60 == 0)
            _eventChannel.Writer.TryWrite((new PlainEvent(EventType.Profile), 60));

        return null;
    }
}

internal class MinimalApp : IRunnableExample
{
    public void Run()
    {
        var core = new Core<NullWorld, EmptyComponentType>(new NullWorld(), new NullRenderer());
        var watcherSystem = new WatcherSystem(10);
        core.AddGameSystem(watcherSystem);
        core.AddEntity(new NullEntity());

        var scheduler = new Scheduler(16, 16, core);
        scheduler.AddEventSources(watcherSystem.EventOutput);
        scheduler.AddEventSink(core, EventType.Shutdown);

        // Alternative solution: enqueue an event which fires after a given time
        // scheduler.EnqueueEvent((new PlainEvent(EventType.Profile), 12 * 16));

        // Run it
        scheduler.SimulationLoop();

        // It should terminate after 12 ticks of 16ms simulated time each.
    }
}