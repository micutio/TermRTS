namespace TermRTS.Examples.Greenery.Event;

public readonly record struct RenderOptionEvent(RenderMode RenderMode) : IEvent
{
    #region IEvent Members

    public EventType Type()
    {
        return EventType.Custom;
    }

    #endregion
}