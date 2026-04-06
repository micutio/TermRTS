namespace TermRTS.Event;

// TODO: Check whether this interface can be internal.

public interface IEvent
{
    Type EvtType { get; }

    ulong TriggerTime { get; }
}