namespace TermRTS;

public class KeyInputEvent : IEvent
{
    public KeyInputEvent(ConsoleKeyInfo info)
    {
        Info = info;
    }

    public EventType Type()
    {
        return EventType.KeyInput;
    }

    public ConsoleKeyInfo Info { get; }
}
