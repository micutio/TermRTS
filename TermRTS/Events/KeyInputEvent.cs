namespace TermRTS;

public class KeyInputEvent : IEvent
{
    private readonly ConsoleKeyInfo _info;

    public KeyInputEvent(ConsoleKeyInfo info)
    {
        _info = info;
    }

    public EventType Type()
    {
        return EventType.KeyInput;
    }

    public ConsoleKeyInfo Info { get { return _info; } }
}
