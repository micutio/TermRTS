namespace TermRTS.Event;

public interface IEvent
{
    Type EvtType { get; }

    ulong TriggerTime { get; }
}