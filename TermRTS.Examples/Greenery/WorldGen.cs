using SimplexNoise;

namespace TermRTS.Examples.Greenery;

public interface IWorldGen
{
    public byte[,] Generate(int worldWidth, int worldHeight);
}

public class VoronoiWorld(int cellCount, int jiggle, int seed = 0) : IWorldGen
{
    private readonly Random _rng = new(seed);

    public byte[,] Generate(int worldWidth, int worldHeight)
    {
        Noise.Seed = seed;

        // step 1: randomly sample <cellCount> coordinates of the grid as voronoi cell seeds
        var voronoiCells = new (int, int)[cellCount];
        for (var i = 0; i < cellCount; i += 1)
            voronoiCells[i] = (_rng.Next(worldWidth), _rng.Next(worldHeight));

        // step 2: for each voronoi cell, determine whether it's going to be water or land
        var landWaterMap = _rng.GetItems([3, 4], cellCount);

        // step 3: associate each grid cell to one of the voronoi cells
        const float scale = .13f;
        var cellElevations = new int[worldWidth, worldHeight];
        var coastalSlopes = new double[worldWidth, worldHeight];
        var jiggleNoise = Noise.Calc2D(worldWidth, worldHeight, scale);

        for (var y = 0; y < worldHeight; y += 1)
        for (var x = 0; x < worldWidth; x += 1)
        {
            var jiggledX = x + (125.5 - jiggleNoise[x, y]) / 255.0f * jiggle;
            var jiggledY = y + (125.5 - jiggleNoise[x, y]) / 255.0f * jiggle;

            var minDist = double.MaxValue;
            for (var i = 0; i < cellCount; i += 1)
            {
                var vX = voronoiCells[i].Item1;
                var vY = voronoiCells[i].Item2;
                var dist = Math.Sqrt(Math.Pow(vX - jiggledX, 2.0f) + Math.Pow(vY - jiggledY, 2.0f));

                if (minDist < dist) continue;

                minDist = dist;
                cellElevations[x, y] = landWaterMap[i];
            }
        }

        // Step 4: Generate coastal slopes for each voronoi cell.
        // 4.1 For each Land cell with adjacent Water cell, set elevation to 0 a.k.a. "Boundary"
        //     Push all boundary cells onto a queue.
        var q = new Queue<(int, int)>();
        for (var y = 1; y < worldHeight - 1; y += 1)
        for (var x = 1; x < worldWidth - 1; x += 1)
        {
            if (cellElevations[x, y] != 4) continue;

            if (cellElevations[x, y - 1] != 3 && // north
                cellElevations[x + 1, y] != 3 && // east
                cellElevations[x, y + 1] != 3 && // south
                cellElevations[x - 1, y] != 3) continue; // west

            coastalSlopes[x, y] = 0.0;
            q.Enqueue((x, y));
        }

        // 4.2 For all other cells, set elevation to 9
        for (var y = 0; y < worldHeight; y += 1)
        for (var x = 0; x < worldWidth; x += 1)
            if (!q.Contains((x, y)))
                coastalSlopes[x, y] = 9.0;

        // 4.3 WHILE queue is NOT empty: deque cell C and set all neighbours to Min(C.elevation + 1, N.elevation)
        //                               enqueue all neighbors with updated elevation
        (int, int)[] directions = [(0, -1), (1, 0), (0, 1), (-1, 0)];
        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            var elevation = coastalSlopes[x, y];
            foreach (var (dirX, dirY) in directions)
            {
                var neighX = x + dirX;
                var neighY = y + dirY;

                if (neighX < 0
                    || neighX >= worldWidth
                    || neighY < 0
                    || neighY >= worldHeight
                    || coastalSlopes[neighX, neighY] <= elevation) continue;

                coastalSlopes[neighX, neighY] = Math.Min(elevation + 1, 9);

                if (elevation < 8)
                    q.Enqueue((neighX, neighY));
            }
        }

        // Step 5: for each voronoi land cell, apply perlin or simplex noise to generate height
        var noiseField = Noise.Calc2D(worldWidth, worldHeight, 0.025f);
        for (var y = 0; y < worldHeight; y += 1)
        for (var x = 0; x < worldWidth; x += 1)
        {
            var baseElevation = cellElevations[x, y] == 4 ? 5.0 : -3.0;
            var slopeFactor = coastalSlopes[x, y] / 9.0;
            var normalizedNoise = noiseField[x, y] / 255.0;
            var elevation = cellElevations[x, y] + baseElevation * slopeFactor * normalizedNoise;

            // debug elevation without noise
            //var elevation = cellElevations[x, y] + baseElevation * slopeFactor;

            // debug: check land-water distribution
            //var elevation = cellElevations[x, y] + baseElevation;

            // debug: check coastal slope values
            // var elevation = 9 * coastalSlopes[x, y];

            cellElevations[x, y] = Convert.ToInt32(Math.Clamp(elevation, 0.0f, 9.0f));
        }

        // optional: apply more techniques from "around the world" to get more appealing shapes

        var world = new byte[worldWidth, worldHeight];
        for (var y = 0; y < worldHeight; y += 1)
        for (var x = 0; x < worldWidth; x += 1)
            world[x, y] = Convert.ToByte(cellElevations[x, y]);
        return world;
    }
}