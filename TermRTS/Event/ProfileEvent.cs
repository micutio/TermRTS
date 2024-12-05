namespace TermRTS.Events;

public readonly record struct ProfileEvent(string ProfileInfo) : IEvent
{
    public EventType Type()
    {
        return EventType.Profile;
    }
}