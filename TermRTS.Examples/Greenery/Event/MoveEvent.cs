using System.Numerics;

namespace TermRTS.Examples.Greenery.Event;

public readonly record struct MoveEvent(int entityId, Vector2 targetPosition) : IEvent
{
    public EventType Type()
    {
        return EventType.Custom;
    }
}