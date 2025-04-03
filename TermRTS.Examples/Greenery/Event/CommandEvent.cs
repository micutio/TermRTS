namespace TermRTS.Examples.Greenery.Event;

public readonly record struct CommandEvent(char[] Command) : IEvent
{
    #region IEvent Members

    public EventType Type()
    {
        return EventType.Custom;
    }

    #endregion
}