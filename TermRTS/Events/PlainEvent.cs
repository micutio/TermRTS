namespace TermRTS;

public class PlainEvent(EventType typ) : IEvent
{
    public EventType Type()
    {
        return typ;
    }
}