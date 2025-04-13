using System.Numerics;

namespace TermRTS.Examples.Greenery.Event;

public readonly record struct Move(int EntityId, Vector2 TargetPosition)
{
}