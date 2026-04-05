namespace TermRTS.Event;

public class EventManager
{
    // The "Fast Lane" - 90% of traffic
    private List<IEvent> _nextTickEvents = new(2048);

    // The "Slow Lane" - for future-dated events
    private PriorityQueue<IEvent, long> _futureEvents = new();

    public void Dispatch(IEvent e)
    {
        if (e.TriggerTime <= _currentTick + 1)
            _nextTickEvents.Add(e);
        else
            _futureEvents.Enqueue(e, e.TriggerTime);
    }

    public void AdvanceTick()
    {
        _currentTick++;

        // 1. Process immediate events
        ProcessList(_nextTickEvents);
        _nextTickEvents.Clear();

        // 2. Pull anything from the Priority Queue that is now "due"
        while (_futureEvents.TryPeek(out _, out long tick) && tick <= _currentTick)
        {
            var e = _futureEvents.Dequeue();
            ProcessEvent(e);
        }
    }
}