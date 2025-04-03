using System.Numerics;
using TermRTS.Algorithms;
using TermRTS.Examples.Greenery.Event;

namespace TermRTS.Examples.Greenery.System;

public class PathFindingSystem(int worldWidth, int worldHeight) : ISimSystem, IEventSink
{
    private readonly Dictionary<int, Vector2> _newTargetPositions = new();

    #region IEventSink Members

    public void ProcessEvent(IEvent evt)
    {
        if (evt.Type() != EventType.Custom || evt is not MoveEvent moveEvent) return;
        _newTargetPositions.Remove(moveEvent.EntityId);
        _newTargetPositions.Add(moveEvent.EntityId, moveEvent.TargetPosition);
    }

    #endregion

    #region ISimSystem Members

    public void ProcessComponents(ulong timeStepSizeMs, in IStorage storage)
    {
        var world = storage.GetSingleForType<WorldComponent>();
        if (world == null) return;

        foreach (var drone in storage.GetAllForType<DroneComponent>())
        {
            TryGeneratePath(world, drone);

            if (drone.Path == null || drone.PathIndex == null) continue;

            ProcessDronePathing(drone, timeStepSizeMs);
        }
    }

    #endregion

    private static void ProcessDronePathing(DroneComponent drone, ulong timeStepSize)
    {
        if (drone.Path == null || drone.PathIndex == null) return;

        if (drone.PathIndex == drone.Path.Count - 1) drone.ResetPath();

        var nextPosition = drone.Path[(int)(drone.PathIndex + 1)];
        var distFromDroneToNext = Vector2.Distance(drone.Position, nextPosition);
        var distCoveredThisTimeStep = DroneComponent.Velocity / 1000 * timeStepSize;

        if (distCoveredThisTimeStep < distFromDroneToNext)
        {
            var normalizedA = Vector2.Normalize(nextPosition - drone.Position);
            var newDronePosA = drone.Position + normalizedA * distCoveredThisTimeStep;
            drone.Position = newDronePosA;
            return;
        }

        if (drone.PathIndex + 1 == drone.Path.Count - 1)
        {
            // end of the path
            drone.Position = nextPosition;
            drone.ResetPath();
            return;
        }

        var remainingDistB = distCoveredThisTimeStep - distFromDroneToNext;
        var nextNextPosition = drone.Path[(int)drone.PathIndex + 2];
        var normalizedB = Vector2.Normalize(nextNextPosition - nextPosition);
        var newDronePosB = nextPosition + normalizedB * remainingDistB;
        drone.Position = newDronePosB;
        drone.Path.RemoveAt(0);
        drone.GeneratePathVisual();
    }

    private void TryGeneratePath(WorldComponent world, DroneComponent drone)
    {
        if (!_newTargetPositions.Remove(drone.EntityId, out var goalPosition)) return;

        var aStar = new AStar(worldWidth, worldHeight, drone.Position, goalPosition)
        {
            Heuristic = loc =>
            {
                var locCell = world.Cells[(int)loc.X, (int)loc.Y];
                var goalCell = world.Cells[(int)goalPosition.X, (int)goalPosition.Y];
                var penalty = locCell <= 3 ? float.PositiveInfinity : 0;
                return Vector2.Distance(loc, goalPosition) + float.Pow(2f, goalCell) + penalty;
            },
            Weight = (loc, neighbor) =>
            {
                var thisCell = world.Cells[(int)loc.X, (int)loc.Y];
                var neighborCell =
                    world.Cells[(int)neighbor.X, (int)neighbor.Y];

                return neighborCell <= 3
                    ? float.PositiveInfinity // do not go into water
                    : float.Pow(2, neighborCell - thisCell);

                // (world.Cells[(int)loc.X, (int)loc.Y] + neighborCell) * 2;
            }
        };
        var path = aStar.ComputePath();
        if (path == null) return;

        path.Reverse();
        drone.Path = path;
        drone.PathIndex = 0;
    }
}