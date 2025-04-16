namespace TermRTS.Event;

public readonly record struct ScheduledEvent(IEvent Event, ulong ScheduledTime)
{
    public static ScheduledEvent FromEvent(IEvent @event)
    {
        return new ScheduledEvent(@event, 0UL);
    }

    public static ScheduledEvent FromEvent(IEvent @event, ulong scheduledTime)
    {
        return new ScheduledEvent(@event, scheduledTime);
    }

    public static ScheduledEvent From<T>(T scheduledEventData)
    {
        return new ScheduledEvent(new Event<T>(scheduledEventData), 0UL);
    }

    public static ScheduledEvent From<T>(T scheduledEventData, ulong scheduledTime)
    {
        return new ScheduledEvent(new Event<T>(scheduledEventData), scheduledTime);
    }
}