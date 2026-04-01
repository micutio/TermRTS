namespace TermRTS.Examples.Greenery.WorldGen;

public class WorldMath
{
    // These would be set during engine initialization
    public const int ChunkSize = 32;
    public const int WorldWidth = 320; // Must be multiple of ChunkSize
    public const int WorldHeight = 96;
    public const int ChunksAcross = WorldWidth / ChunkSize;

    /// <summary>
    /// Converts world-grid X to wrapped cylinder X.
    /// </summary>
    public static int WrapX(int x)
    {
        return (x % WorldWidth + WorldWidth) % WorldWidth;
    }

    public static int GetChunkIndex(int x, int y)
    {
        // 1. Handle Cylindrical Horizontal Wrap
        // (x % worldWidth + worldWidth) % worldWidth handles negative x safely

        // 2. Convert to Chunk-Space Coordinates
        var cx = WrapX(x) / ChunkSize;
        var cy = Math.Clamp(y, 0, WorldHeight - 1) / ChunkSize;

        // 3. Flatten to 1D Index
        return cy * ChunksAcross + cx;
    }

    // Optimized for ChunkSize = 32
    public static int GetChunkIndexFast(int x, int y)
    {
        // Wrap x for the cylinder
        var wrappedX = (x % WorldWidth + WorldWidth) % WorldWidth;

        // Shift instead of divide
        var cx = wrappedX >> 5;
        var cy = y >> 5;

        return cy * ChunksAcross + cx;
    }

    public static int GetNeighborChunkIndex(int currentIdx, int dx, int dy)
    {
        var cx = currentIdx % ChunksAcross;
        var cy = currentIdx / ChunksAcross;

        // Horizontal wrapping for the cylinder
        var targetCx = (cx + dx + ChunksAcross) % ChunksAcross;

        // Vertical clamping (Poles don't wrap!)
        var targetCy = cy + dy;
        if (targetCy is < 0 or >= WorldHeight / ChunkSize) return -1;

        return targetCy * ChunksAcross + targetCx;
    }

    /// <summary>
    ///     Calculates the distance between two points on a cylindrical world.
    /// </summary>
    /// <param name="worldWidth">Width of the world in cells.</param>
    /// <param name="x1">X-coordinate of the first point.</param>
    /// <param name="y1">Y-coordinate of the first point.</param>
    /// <param name="x2">X-coordinate of the second point.</param>
    /// <param name="y2">Y-coordinate of the second point.</param>
    /// <returns>
    ///     Distance between both points on a cylinder.
    /// </returns>
    public static float GetCylindricalDistance(int x1, int y1, int x2, int y2)
    {
        var dx = Math.Min(Math.Abs(x2 - x1), WorldWidth - Math.Abs(x2 - x1));
        var dy = Math.Abs(y2 - y1);
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    ///     Calculates the distance between two points on a cylindrical world.
    /// </summary>
    /// <param name="worldWidth">Width of the world in cells.</param>
    /// <param name="x1">X-coordinate of the first point.</param>
    /// <param name="y1">Y-coordinate of the first point.</param>
    /// <param name="x2">X-coordinate of the second point.</param>
    /// <param name="y2">Y-coordinate of the second point.</param>
    /// <returns>
    ///     Distance between both points on a cylinder.
    /// </returns>
    public static float GetCylindricalDistance(double x1, double y1, double x2,
        double y2)
    {
        var dx = Math.Min(Math.Abs(x2 - x1), WorldWidth - Math.Abs(x2 - x1));
        var dy = Math.Abs(y2 - y1);
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    ///     Calculates the squared distance between two points on a cylindrical world.
    /// </summary>
    /// <param name="worldWidth">Width of the world in cells.</param>
    /// <param name="x1">X-coordinate of the first point.</param>
    /// <param name="y1">Y-coordinate of the first point.</param>
    /// <param name="x2">X-coordinate of the second point.</param>
    /// <param name="y2">Y-coordinate of the second point.</param>
    /// <returns>
    ///     Distance between both points on a cylinder, squared for faster computation.
    /// </returns>
    public static float GetCylindricalDistanceSq(
        double x1,
        double y1,
        double x2,
        double y2)
    {
        var dx = Math.Min(Math.Abs(x2 - x1), WorldWidth - Math.Abs(x2 - x1));
        var dy = Math.Abs(y2 - y1);
        return (float)(dx * dx + dy * dy);
    }
}