namespace TermRTS.Event;

public enum PersistenceOption
{
    Load,
    Save
}

public readonly record struct PersistenceEvent(PersistenceOption option, string jsonFilePath)
    : IEvent
{
    #region IEvent Members

    public EventType Type()
    {
        return EventType.Persistence;
    }

    #endregion
}