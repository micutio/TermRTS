using System.Numerics;

namespace TermRTS.Examples.Greenery.Event;

public readonly record struct MoveEvent(int EntityId, Vector2 TargetPosition) : IEvent
{
    #region IEvent Members

    public EventType Type()
    {
        return EventType.Custom;
    }

    #endregion
}