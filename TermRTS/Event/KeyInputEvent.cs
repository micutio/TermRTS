namespace TermRTS.Event;

public readonly record struct KeyInputEvent(ConsoleKeyInfo Info) : IEvent
{
    public EventType Type()
    {
        return EventType.KeyInput;
    }
}