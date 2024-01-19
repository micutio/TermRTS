namespace TermRTS;

public class PlainEvent : IEvent
{
    private EventType _type;

    public PlainEvent(EventType typ)
    {
        _type = typ;
    }

    public EventType Type()
    {
        return _type;
    }
}
