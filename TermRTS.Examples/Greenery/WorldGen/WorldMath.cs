namespace TermRTS.Examples.Greenery.WorldGen;

public static class WorldMath
{
    // These would be set during engine initialization
    public const int ChunkSize = 32;
    public const int WorldWidth = 320; // Must be multiple of ChunkSize
    public const int WorldHeight = 96;
    public const int ChunksAcross = WorldWidth / ChunkSize;

    public readonly struct WorldCoord(int x, int y)
    {
        public readonly int WorldX = x;
        public readonly int WorldY = y;

        public int ChunkX => WorldX >> 5;
        public int ChunkY => WorldY >> 5;
        public int LocalX => WorldX & 31;
        public int LocalY => WorldY & 31;

        public int LocalIndex => (LocalY << 5) + LocalX;
    }

    public static (int chunkX, int chunkY, int localX, int localY) ToRelative(int x, int y)
    {
        // 1. Wrap X for the cylinder before calculating
        var wrappedX = (x % WorldWidth + WorldWidth) % WorldWidth;

        // 2. Get Chunk Grid Coordinates (which 'box' is it in?)
        var cx = wrappedX >> 5; // Same as / 32
        var cy = y >> 5;

        // 3. Get Local Coordinates (where inside the 'box'?)
        var lx = wrappedX & 31; // Same as % 32
        var ly = y & 31;

        return (cx, cy, lx, ly);
    }

    public static (int x, int y) ToWorld(int cx, int cy, int lx, int ly)
    {
        var x = (cx << 5) + lx; // Same as (cx * 32) + lx
        var y = (cy << 5) + ly; // Same as (cy * 32) + ly

        return (x, y);
    }

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
    /// <param name="x1">X-coordinate of the first point.</param>
    /// <param name="y1">Y-coordinate of the first point.</param>
    /// <param name="x2">X-coordinate of the second point.</param>
    /// <param name="y2">Y-coordinate of the second point.</param>
    /// <returns>
    ///     Distance between both points on a cylinder.
    /// </returns>
    public static float GetCylindricalDistanceSq(float x1, float y1, float x2, float y2)
    {
        var dx = Math.Min(Math.Abs(x2 - x1), WorldWidth - Math.Abs(x2 - x1));
        var dy = Math.Abs(y2 - y1);
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}

public readonly struct WorldView(ReadOnlyMemory<float> masterBuffer)
{
    public float GetElevation(int x, int y)
    {
        // Handle horizontal wrap for the cylinder
        var wrappedX = (x % WorldMath.WorldWidth + WorldMath.WorldWidth) % WorldMath.WorldWidth;

        // Clamp Y to the poles
        var clampedY = Math.Clamp(y, 0, WorldMath.WorldHeight - 1);

        return masterBuffer.Span[clampedY * WorldMath.WorldWidth + wrappedX];
    }
}