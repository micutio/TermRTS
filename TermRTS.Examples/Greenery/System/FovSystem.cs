using TermRTS.Algorithms;

namespace TermRTS.Examples.Greenery.System;

public class FovSystem : SimSystem
{
    public override void ProcessComponents(ulong timeStepSize, in IStorage storage)
    {
        // TODO: Switch Fov algorithms at runtime.
        
        storage.GetForType(typeof(WorldComponent), out var worldComponents);
        foreach (var worldComponent in worldComponents)
            if (worldComponent is WorldComponent world)
            {
                storage.GetForType(typeof(FovComponent), out var fovComponents);
                foreach (var fovComponent in fovComponents)
                    if (fovComponent is FovComponent fov)
                    {
                        for (var y = 0; y < fov.Height; y++)
                        for (var x = 0; x < fov.Width; x++)
                            fov.Cells[x, y] = false;
                        
                        storage.GetForType(typeof(DroneComponent), out var droneComponents);
                        foreach (var droneComponent in droneComponents)
                            if (droneComponent is DroneComponent drone)
                            {
                                var droneX = (int)drone.Position.X;
                                var droneY = (int)drone.Position.Y;
                                var droneFov =
                                    Fov.BasicRaycast(
                                        droneX,
                                        droneY,
                                        10,
                                        (x, y) => world.Cells[x, y] > world.Cells[droneX, droneY]);
                                foreach (var (x, y) in droneFov) fov.Cells[x, y] = true;
                            }
                    }
            }
    }
}