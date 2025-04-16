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

/// <summary>
/// Log messages concerning the simulation system itself.
/// Not to be confused with game- or simulation-specific logs.
/// Implementers may choose to expose these to the user, e.g.: confirming of loading and saving.
/// </summary>
/// <param name="Content">The actual log message.</param>
public readonly record struct SystemLog(string Content)
{
}

public readonly struct Shutdown
{
}