using System.Numerics;

namespace TermRTS.Examples.Greenery.Event;

public readonly record struct MoveEvent(int EntityId, Vector2 TargetPosition) : IEvent
{
    public EventType Type()
    {
        return EventType.Custom;
    }
}