namespace TermRTS.Event;

public readonly record struct ProfileEvent(string ProfileInfo) : IEvent
{
    #region IEvent Members

    public EventType Type()
    {
        return EventType.Profile;
    }

    #endregion
}