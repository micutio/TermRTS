using System.Numerics;
using TermRTS.Algorithms;
using TermRTS.Examples.Greenery.Event;

namespace TermRTS.Examples.Greenery.System;

public class PathFindingSystem(int worldWidth, int worldHeight) : SimSystem, IEventSink
{
    private readonly Dictionary<int, Vector2> _newTargetPositions = new();
    
    public override void ProcessComponents(ulong timeStepSize, in IStorage storage)
    {
        storage.GetForType(typeof(WorldComponent), out var worldComponents);
        foreach (var worldComponent in worldComponents)
            if (worldComponent is WorldComponent world)
            {
                storage.GetForType(typeof(DroneComponent), out var droneComponents);
                foreach (var droneComponent in droneComponents)
                    if (droneComponent is DroneComponent drone)
                    {
                        TryGeneratePath(world, drone);
                        
                        if (drone.Path == null || drone.PathIndex == null) continue;
                        
                        ProcessDronePathing(drone, timeStepSize);
                    }
            }
    }
    
    public void ProcessEvent(IEvent evt)
    {
        if (evt.Type() != EventType.Custom || evt is not MoveEvent moveEvent) return;
        _newTargetPositions.Remove(moveEvent.entityId);
        _newTargetPositions.Add(moveEvent.entityId, moveEvent.targetPosition);
    }
    
    private static void ProcessDronePathing(DroneComponent drone, ulong timeStepSize)
    {
        if (drone.Path == null || drone.PathIndex == null) return;
        
        if (drone.PathIndex == drone.Path.Count - 1) drone.ResetPath();
        
        var nextPosition = drone.Path[(int)(drone.PathIndex + 1)];
        var distFromDroneToNext = Vector2.Distance(drone.Position, nextPosition);
        var distCoveredThisTimeStep = DroneComponent.Velocity / 1000 * timeStepSize;
        
        if (distCoveredThisTimeStep < distFromDroneToNext)
        {
            var remainingDistA = distFromDroneToNext - distCoveredThisTimeStep;
            var normalizedA = Vector2.Normalize(nextPosition - drone.Position);
            var newDronePosA = drone.Position + normalizedA * remainingDistA;
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
        drone.GeneratePathVisual();
    }
    
    private void TryGeneratePath(WorldComponent world, DroneComponent drone)
    {
        if (!_newTargetPositions.TryGetValue(drone.EntityId, out var goalPosition)) return;
        
        var aStar = new AStar(worldWidth, worldHeight, drone.Position, goalPosition)
        {
            Heuristic = loc =>
            {
                var locCell = world.Cells[Convert.ToInt32(loc.X), Convert.ToInt32(loc.Y)];
                var penalty = locCell <= 3 ? float.PositiveInfinity : 0;
                return Vector2.Distance(loc, goalPosition) + penalty;
            },
            Weight = (loc, neighbor) =>
            {
                var neighborCell = world.Cells[Convert.ToInt32(neighbor.X), Convert.ToInt32(neighbor.Y)];
                
                if (neighborCell <= 3) return float.PositiveInfinity; // Do not go into water
                
                return world.Cells[Convert.ToInt32(loc.X), Convert.ToInt32(loc.Y)] +
                       neighborCell;
            }
        };
        var path = aStar.ComputePath();
        if (path == null) return;
        
        drone.Path = path;
        drone.PathIndex = 0;
    }
}