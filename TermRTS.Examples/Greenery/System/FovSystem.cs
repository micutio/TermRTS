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
                (x, y) =>
                {
                    var wrappedX = GetWrappedX(fov.WorldWidth, x);
                    if (y <= 0 || y >= fov.WorldHeight) return true;

                    return world.Cells[wrappedX, y] > world.Cells[droneX, droneY];
                });
            foreach (var (x, y) in _fov.VisibleCells)
                fov.Cells[GetWrappedX(fov.WorldWidth, x), y] = true;
        }
    }

    #endregion

    /// <summary>
    ///     Calculates an X-coordinate for a cylindrical grid that
    ///     wraps around the left and right edges.
    /// </summary>
    /// <param name="worldWidth">Width of the world in grid cells.</param>
    /// <param name="x">X-Coordinate to convert to wrapping around.</param>
    /// <returns>X, if is within the bounds of the world, wrapped coordinate otherwise.</returns>
    private static int GetWrappedX(int worldWidth, int x)
    {
        return (x % worldWidth + worldWidth) % worldWidth;
    }
}