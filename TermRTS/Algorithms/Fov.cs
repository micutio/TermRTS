namespace TermRTS.Algorithms;

public static class Fov
{
    // Basic Raycasting
    public static HashSet<(int x, int y)> BasicRaycast(
        int startX,
        int startY,
        int range,
        Func<int, int, bool> isWall)
    {
        var visibleCells = new HashSet<(int x, int y)>();

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

                visibleCells.Add((gridX, gridY));
            }
        }

        return visibleCells;
    }


    // Recursive Shadowcasting (Octant-based)
    public static HashSet<(int x, int y)> RecursiveShadowcasting(
        int startX,
        int startY,
        int range,
        Func<int, int, bool> isWall)
    {
        var visibleCells = new HashSet<(int x, int y)>();
        visibleCells.Add((startX, startY)); // Start is always visible

        for (var octant = 0; octant < 8; octant++)
            CastOctant(visibleCells, startX, startY, range, 1, 1.0f, 0.0f, octant, isWall);

        return visibleCells;
    }

    private static void CastOctant(
        HashSet<(int x, int y)> visibleCells,
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
            visibleCells.Add((tx, ty));

            if (isWall(tx, ty))
                CastOctant(visibleCells, startX, startY, range, x + 1, slopeStart, prevSlope, octant, isWall);
            else
                slopeStart = currentSlope;
        }
    }
}

// Example usage:
public static class Example
{
    public static void Main(string[] args)
    {
        // Example map (true = wall, false = empty)
        var map = new bool[10, 10];
        map[2, 2] = true;
        map[2, 3] = true;
        map[2, 4] = true;
        map[5, 5] = true;

        Func<int, int, bool> isWall = (x, y) =>
            x < 0 || y < 0 || x >= map.GetLength(0) || y >= map.GetLength(1) || map[x, y];

        var startX = 1;
        var startY = 1;
        var range = 5;

        var basicFOV = Fov.BasicRaycast(startX, startY, range, isWall);
        var recursiveFOV = Fov.RecursiveShadowcasting(startX, startY, range, isWall);

        Console.WriteLine("Basic Raycast FOV:");
        foreach (var cell in basicFOV) Console.WriteLine($"({cell.x}, {cell.y})");

        Console.WriteLine("\nRecursive Shadowcasting FOV:");
        foreach (var cell in recursiveFOV) Console.WriteLine($"({cell.x}, {cell.y})");
    }
}