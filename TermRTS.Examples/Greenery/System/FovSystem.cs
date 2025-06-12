using TermRTS.Algorithms;

namespace TermRTS.Examples.Greenery.System;

public class FovSystem : ISimSystem
{
    #region ISimSystem Members

    public void ProcessComponents(ulong timeStepSizeMs, in IReadonlyStorage storage)
    {
        // TODO: Switch Fov algorithms at runtime.
        var world = storage.GetSingleForType<WorldComponent>();
        if (world == null) return;
        var fov = storage.GetSingleForType<FovComponent>();
        if (fov == null) return;

        for (var y = 0; y < fov.WorldHeight; y++)
        for (var x = 0; x < fov.WorldWidth; x++)
            fov.Cells[x, y] = false;

        foreach (var dronePos in storage.GetAllForType<DroneComponent>()
                     .Select(drone => drone.Position))
        {
            var droneX = (int)dronePos.X;
            var droneY = (int)dronePos.Y;
            var droneFov =
                Fov.BasicRaycast(
                    droneX,
                    droneY,
                    10,
                    (x, y) => world.Cells[x, y] > world.Cells[droneX, droneY]);
            foreach (var (x, y) in droneFov) fov.Cells[x, y] = true;
        }
    }

    #endregion
}