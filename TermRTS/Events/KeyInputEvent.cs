namespace TermRTS;

public class KeyInputEvent(ConsoleKeyInfo info) : IEvent
{
    public ConsoleKeyInfo Info { get; } = info;
    
    public EventType Type()
    {
        return EventType.KeyInput;
    }
}