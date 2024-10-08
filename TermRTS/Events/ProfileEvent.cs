namespace TermRTS;

public class ProfileEvent : IEvent
{
    public ProfileEvent(string profileInfo)
    {
        ProfileInfo = profileInfo;
    }
    
    public string ProfileInfo { get; private set; }
    
    public EventType Type()
    {
        return EventType.Profile;
    }
}