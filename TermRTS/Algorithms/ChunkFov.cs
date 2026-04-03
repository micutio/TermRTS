namespace TermRTS.Algorithms;

public class ChunkFov
{
    public HashSet<Pos> VisibleCells { get; } = new(50);

    // Basic Raycasting
    public void BasicRaycast<T>(
        int startX,
        int startY,
        int range,
        T accessor,
        Func<int, int, T, bool> isWall) where T : allows ref struct
    {
        VisibleCells.Clear();

        for (var angle = 0; angle < 360; angle++)
        {
            var radians = angle * Math.PI / 180d;
            var dx = Math.Cos(radians);
            var dy = Math.Sin(radians);

            var x = startX + 0.5; // Start in the center of the cell
            var y = startY + 0.5;

            for (var i = 0; i < range; i++)
            {
                x += dx;
                y += dy;

                var gridX = (int)Math.Floor(x);
                var gridY = (int)Math.Floor(y);

                // Wrap X coordinate for cylindrical world, clamp Y coordinate
                var wrappedGridX = (gridX % 320 + 320) % 320; // WorldWidth = 320
                var clampedGridY = Math.Clamp(gridY, 0, 95); // WorldHeight = 96, so max Y = 95

                if (isWall(wrappedGridX, clampedGridY, accessor)) break; // Stop the ray if it hits a wall

                VisibleCells.Add(new Pos(wrappedGridX, clampedGridY));
            }
        }
    }


    // Recursive Shadowcasting (Octant-based)
    public void RecursiveShadowcasting<T>(
        int startX,
        int startY,
        int range,
        T accessor,
        Func<int, int, T, bool> isWall)
    {
        VisibleCells.Clear();
        VisibleCells.Add(new Pos(startX, startY)); // Start is always visible

        for (var octant = 0; octant < 8; octant++)
            CastOctant(startX, startY, range, 1, 1.0f, 0.0f, octant, accessor, isWall);
    }

    private void CastOctant<T>(
        int startX,
        int startY,
        int range,
        int x,
        float slopeStart,
        float slopeEnd,
        int octant,
        T accessor,
        Func<int, int, T, bool> isWall)
    {
        for (var y = x; y <= range; y++)
        {
            var currentSlope = y / (x + 0.5f);

            if (currentSlope < slopeEnd) continue;

            var prevSlope = (y - 0.5f) / x;

            if (prevSlope > slopeStart) break;

            var tx = startX;
            var ty = startY;

            switch (octant)
            {
                case 0:
                    tx += x;
                    ty -= y;
                    break;
                case 1:
                    tx += y;
                    ty -= x;
                    break;
                case 2:
                    tx += y;
                    ty += x;
                    break;
                case 3:
                    tx += x;
                    ty += y;
                    break;
                case 4:
                    tx -= x;
                    ty += y;
                    break;
                case 5:
                    tx -= y;
                    ty += x;
                    break;
                case 6:
                    tx -= y;
                    ty -= x;
                    break;
                case 7:
                    tx -= x;
                    ty -= y;
                    break;
            }

            if (tx < 0 || ty < 0) continue; //Bounds Check

            // Wrap X coordinate for cylindrical world, clamp Y coordinate
            var wrappedTx = (tx % 320 + 320) % 320; // WorldWidth = 320
            var clampedTy = Math.Clamp(ty, 0, 95); // WorldHeight = 96, so max Y = 95

            VisibleCells.Add(new Pos(wrappedTx, clampedTy));

            if (isWall(wrappedTx, clampedTy, accessor))
                CastOctant(
                    startX,
                    startY,
                    range,
                    x + 1,
                    slopeStart,
                    prevSlope,
                    octant,
                    accessor,
                    isWall);
            else
                slopeStart = currentSlope;
        }
    }
}