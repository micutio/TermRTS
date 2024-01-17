using System.Threading.Channels;

namespace TermRTS.Examples.Testing;

public class NullWorld : TermRTS.IWorld
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


public class WatcherSystem : System<NullWorld, EmptyComponentType>
{
private int _remainingTicks;
private Channel<(IEvent, UInt128)> _eventChannel;
public ChannelReader<(IEvent, UInt128)> EventOutput;

public WatcherSystem(int remainingTicks)
{
_remainingTicks = remainingTicks;
_eventChannel = Channel.CreateUnbounded<(IEvent, UInt128)>();
EventOutput = _eventChannel.Reader;
}

public override Dictionary<EmptyComponentType, IComponent>? ProcessComponents(UInt128 timeStepSize, EntityBase<EmptyComponentType> thisEntityComponents, List<EntityBase<EmptyComponentType>> otherEntityComponents, ref NullWorld world)
{
_remainingTicks -= 1;

if (_remainingTicks == 0)
{
_eventChannel.Writer.TryWrite((new PlainEvent(EventType.Shutdown), 0));
}

return new Dictionary<EmptyComponentType, IComponent>();
}

}

public static void Main(Core<NullWorld, EmptyComponentType> core)
{
// Setup Scheduler
var watcherSystem = new WatcherSystem(remainingTicks: 12);
var scheduler = new Scheduler(16, 16, core);
scheduler.AddEventSources(watcherSystem.EventOutput);
core.AddGameSystem(watcherSystem);

// Run it
scheduler.GameLoop();

// It should terminate after 12 ticks of 16ms simulated time each.
UInt128 finalTime = 12 * 16;
}
