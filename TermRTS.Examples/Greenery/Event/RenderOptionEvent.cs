namespace TermRTS.Examples.Greenery.Event;

public readonly record struct RenderOptionEvent(RenderMode RenderMode) : IEvent
{
    public EventType Type()
    {
        return EventType.Custom;
    }
}