namespace TermRTS.Event;

public interface IEvent
{
    Type EvtType { get; }
}

public readonly record struct Event<TPayload>(TPayload Payload) : IEvent
{
    public void Deconstruct(out TPayload payload)
    {
        payload = Payload;
    }

    public Type EvtType => typeof(TPayload);
}

public readonly struct Shutdown
{
}