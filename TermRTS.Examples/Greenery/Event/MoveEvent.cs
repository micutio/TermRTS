using System.Numerics;
using TermRTS.Event;

namespace TermRTS.Examples.Greenery.Event;

public readonly record struct Move(int EntityId, Vector2 TargetPosition)
{
}