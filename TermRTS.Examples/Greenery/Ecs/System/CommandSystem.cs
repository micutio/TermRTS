using TermRTS.Ecs;
using TermRTS.Event;
using TermRTS.Examples.Greenery.Command;
using TermRTS.Storage;

namespace TermRTS.Examples.Greenery.Ecs.System;

public class CommandSystem : ISimSystem, IEventSink
{
    private readonly Queue<ICommand> _cmdQueue = new();

    public void ProcessEvent(IEvent evt)
    {
        if (evt is Event<ICommand>(var eventCmd))
        {
            _cmdQueue.Enqueue(eventCmd);
        }

    }

    public void ProcessComponents(
        ulong timeStepSizeMs,
        in IReadonlyStorage storage,
        in List<ScheduledEvent> emittedEvents)
    {
        while (_cmdQueue.Count > 0)
        {
            var cmd = _cmdQueue.Dequeue();
            cmd.Execute(timeStepSizeMs, storage, emittedEvents);
        }
        _cmdQueue.Clear();
    }
}