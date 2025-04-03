namespace TermRTS.Event;

public readonly record struct PlainEvent(EventType Typ) : IEvent
{
    #region IEvent Members

    public EventType Type()
    {
        return Typ;
    }

    #endregion
}