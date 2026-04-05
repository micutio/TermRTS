namespace TermRTS.Event;

public readonly record struct ScheduledEvent(IEvent Event)
{
    public static ScheduledEvent From<T>(T scheduledEventData)
    {
        return new ScheduledEvent(new Event<T>(scheduledEventData, 0UL));
    }

    public static ScheduledEvent From<T>(T scheduledEventData, ulong scheduledTime)
    {
        return new ScheduledEvent(new Event<T>(scheduledEventData, scheduledTime));
    }
}