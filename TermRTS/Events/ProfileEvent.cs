namespace TermRTS;

public class ProfileEvent(string profileInfo) : IEvent
{
    public string ProfileInfo { get; } = profileInfo;
    
    public EventType Type()
    {
        return EventType.Profile;
    }
}