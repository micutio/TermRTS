namespace TermRTS.Algorithms;

public record struct Pos(int X, int Y);

public class Fov
{
    public HashSet<Pos> VisibleCells { get; } = new(50);

    // Basic Raycasting
    public void BasicRaycast(
        int startX,
        int startY,
        int range,
        Func<int, int, bool> isWall)
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

                if (isWall(gridX, gridY)) break; // Stop the ray if it hits a wall

                VisibleCells.Add(new Pos(gridX, gridY));
            }
        }
    }


    // Recursive Shadowcasting (Octant-based)
    public void RecursiveShadowcasting(
        int startX,
        int startY,
        int range,
        Func<int, int, bool> isWall)
    {
        VisibleCells.Clear();
        VisibleCells.Add(new Pos(startX, startY)); // Start is always visible

        for (var octant = 0; octant < 8; octant++)
            CastOctant(startX, startY, range, 1, 1.0f, 0.0f, octant, isWall);
    }

    private void CastOctant(
        int startX,
        int startY,
        int range,
        int x,
        float slopeStart,
        float slopeEnd,
        int octant,
        Func<int, int, bool> isWall)
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
            VisibleCells.Add(new Pos(tx, ty));

            if (isWall(tx, ty))
                CastOctant(startX, startY, range, x + 1, slopeStart, prevSlope,
                    octant, isWall);
            else
                slopeStart = currentSlope;
        }
    }
}