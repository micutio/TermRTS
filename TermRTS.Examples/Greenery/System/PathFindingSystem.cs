using System.Numerics;

namespace TermRTS.Examples.Greenery.System;

public class PathFindingSystem : SimSystem
{
    public override void ProcessComponents(ulong timeStepSize, in IStorage storage)
    {
        storage.GetForType(typeof(DroneComponent), out var droneComponents);
        foreach (var droneComponent in droneComponents)
            if (droneComponent is DroneComponent drone)
            {
                if (drone.Path == null || drone.PathIndex == null) continue;
                
                if (drone.PathIndex == drone.Path.Count - 1) drone.ResetPath();
                
                var lastPosition = drone.Path[(int)drone.PathIndex];
                var nextPosition = drone.Path[(int)drone.PathIndex + 1];
                var distToNextPosX = nextPosition.X - drone.Position.X;
                var distToNextPosY = nextPosition.Y - drone.Position.Y;
                
                var deltaDistInM = DroneComponent.Velocity / 1000.0f * timeStepSize;
            }
    }
}