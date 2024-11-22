namespace TermRTS.Events;

public readonly record struct PlainEvent(EventType Typ) : IEvent
{
    public EventType Type()
    {
        return Typ;
    }
}