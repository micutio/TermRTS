namespace TermRTS.Event;

public readonly record struct Event<TPayload>(TPayload Payload) : IEvent
{
    #region IEvent Members

    public Type EvtType => typeof(TPayload);

    #endregion

    public void Deconstruct(out TPayload payload)
    {
        payload = Payload;
    }
}

public readonly record struct Profile(string ProfileInfo)
{
}

public readonly record struct LogMessage(string Content)
{
}

public readonly struct Shutdown
{
}