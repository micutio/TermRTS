using TermRTS.Algorithms;
using TermRTS.Storage;

namespace TermRTS.Examples.Greenery.System;

public class FovSystem : ISimSystem
{
    private readonly Fov _fov = new();

    #region ISimSystem Members

    public void ProcessComponents(ulong timeStepSizeMs, in IReadonlyStorage storage)
    {
        // TODO: Switch Fov algorithms at runtime.
        if (!storage.TryGetSingleForType<WorldComponent>(out var world) || world == null) return;
        if (!storage.TryGetSingleForType<FovComponent>(out var fov) || fov == null) return;

        for (var y = 0; y < fov.WorldHeight; y++)
        for (var x = 0; x < fov.WorldWidth; x++)
            fov.Cells[x, y] = false;

        foreach (var dronePos in storage.GetAllForType<DroneComponent>()
                     .Select(drone => drone.Position))
        {
            var droneX = (int)dronePos.X;
            var droneY = (int)dronePos.Y;
            _fov.BasicRaycast(
                droneX,
                droneY,
                10,
                (x, y) => world.Cells[x, y] > world.Cells[droneX, droneY]);
            foreach (var (x, y) in _fov.VisibleCells) fov.Cells[x, y] = true;
        }
    }

    #endregion
}