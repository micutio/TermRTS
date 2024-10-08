using System.Threading.Channels;

namespace TermRTS.Examples.Testing;

internal enum EmptyComponentType
{
    Empty
}

internal class NullRenderer : IRenderer
{
    public void RenderComponents(in IStorage components, double timeStepSizeMs, double howFarIntoNextFrameMs)
    {
        // Console.WriteLine($"Rendering null-entity at {howFarIntoNextFrameMs} ms into next frame.");
    }
    
    public void FinalizeRender()
    {
    }
    
    public void Shutdown()
    {
    }
}

internal class WatcherSystem : SimSystem
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
    
    public override void ProcessComponents(ulong timeStepSizeMs, in IStorage storage)
    {
        _remainingTicks -= 1;
        Console.WriteLine($"[WatcherSystem] remaining ticks: {_remainingTicks}");
        
        if (_remainingTicks == 0)
            _eventChannel.Writer.TryWrite((new PlainEvent(EventType.Shutdown), 0));
        
        if (_remainingTicks % 60 == 0)
            _eventChannel.Writer.TryWrite((new PlainEvent(EventType.Profile), 60));
    }
}

internal class MinimalApp : IRunnableExample
{
    public void Run()
    {
        var core = new Core(new NullRenderer());
        var watcherSystem = new WatcherSystem(10);
        core.AddSimSystem(watcherSystem);
        core.AddEntity(new EntityBase());
        
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