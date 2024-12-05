namespace TermRTS.Examples.Greenery.Event;

public readonly record struct CommandEvent(char[] Command) : IEvent
{
    public EventType Type()
    {
        return EventType.Custom;
    }
}