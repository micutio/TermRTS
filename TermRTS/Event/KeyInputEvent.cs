namespace TermRTS.Event;

public readonly record struct KeyInputEvent(ConsoleKeyInfo Info) : IEvent
{
    #region IEvent Members

    public EventType Type()
    {
        return EventType.KeyInput;
    }

    #endregion
}