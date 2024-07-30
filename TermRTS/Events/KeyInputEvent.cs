namespace TermRTS;

public class KeyInputEvent : IEvent
{
    public KeyInputEvent(ConsoleKeyInfo info)
    {
        Info = info;
    }

    public ConsoleKeyInfo Info { get; }

    public EventType Type()
    {
        return EventType.KeyInput;
    }
}