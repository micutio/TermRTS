namespace TermRTS.Event;

public readonly record struct Event<TPayload>(TPayload Payload) : IEvent
{
    public void Deconstruct(out TPayload payload)
    {
        payload = Payload;
    }

    public Type EvtType => typeof(TPayload);
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