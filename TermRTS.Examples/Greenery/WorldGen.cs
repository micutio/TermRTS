using System.Numerics;
using SimplexNoise;

namespace TermRTS.Examples.Greenery;

// Refer to link below for a nice layered noise map implementation:
// https://github.com/SebLague/Procedural-Landmass-Generation/blob/master/Proc%20Gen%20E03/Assets/Scripts/Noise.cs

public interface IWorldGen
{
    public byte[,] Generate(int worldWidth, int worldHeight, float landRatio);
}

public class VoronoiWorld(int cellCount, int seed = 0) : IWorldGen
{
    private readonly Random _rng = new(seed);

    #region IWorldGen Members

    public byte[,] Generate(int worldWidth, int worldHeight, float landRatio)
    {
        Noise.Seed = seed;

        // step 1: randomly sample <cellCount> coordinates of the grid as voronoi cell seeds
        var voronoiCells = new (int, int)[cellCount];
        for (var i = 0; i < cellCount; i += 1)
            voronoiCells[i] = (_rng.Next(worldWidth), _rng.Next(worldHeight));

        // step 2: assign plate types and motions, and infer basic water/land by plate type
        var plateTypes = GeneratePlateTypes(cellCount, _rng, landRatio); // true = continental, false = oceanic
        var plateMotions = GeneratePlateMotions(cellCount, _rng);

        var landWaterMap = new int[cellCount];
        for (var i = 0; i < cellCount; i += 1)
            landWaterMap[i] = plateTypes[i] ? 4 : 3;

        // step 3: associate each grid cell to one of the voronoi cells
        const int jiggle = 15;
        var (cellElevations, plateIndex) = GenerateLandWaterDistribution(
            worldWidth, worldHeight, jiggle, voronoiCells, landWaterMap);

        // Step 4: Generate coastal slopes for each voronoi cell.
        var coastalSlopes = GenerateSlopedCoasts(worldWidth, worldHeight, in cellElevations);

        // Step 5: Compute plate tectonics influence (mountains/trenches along plate boundaries)
        var tectonicAdjustment = ComputePlateTectonicHeight(
            worldWidth,
            worldHeight,
            plateIndex,
            voronoiCells,
            plateTypes,
            plateMotions);

        // Step 6: for each voronoi land cell, apply perlin or simplex noise to generate height
        const float noiseScale = 1.0f;
        const int octaves = 4;
        const float persistance = 0.75f; //?
        const float lacunarity = 1.8f; //?
        var offset = new Vector2(1f, 1f);
        var noiseField = GenerateNoiseMap(
            worldWidth,
            worldHeight,
            seed,
            noiseScale,
            octaves,
            persistance,
            lacunarity,
            offset);

        // Step 6: Apply noise and slopes to elevation map.
        for (var y = 0; y < worldHeight; y += 1)
            for (var x = 0; x < worldWidth; x += 1)
            {
                var baseElevation = cellElevations[x, y] == 4 ? 5.0 : -3.0;
                var slopeFactor = coastalSlopes[x, y] / 9.0;
                var normalizedNoise = noiseField[x, y] / 255.0;
                var tectonic = tectonicAdjustment[x, y];

                var elevation = cellElevations[x, y] + baseElevation * slopeFactor * normalizedNoise + tectonic;

                // debug elevation without noise
                //var elevation = cellElevations[x, y] + baseElevation * slopeFactor + tectonic;
                // debug: check land-water distribution
                // var elevation = cellElevations[x, y] + baseElevation + tectonic;
                // debug: check coastal slope values
                // var elevation = 9 * coastalSlopes[x, y] + tectonic;
                cellElevations[x, y] = Convert.ToInt32(Math.Clamp(elevation, 0.0f, 9.0f));
            }

        // optional: apply more techniques from "around the world" to get more appealing shapes

        var world = new byte[worldWidth, worldHeight];
        for (var y = 0; y < worldHeight; y += 1)
            for (var x = 0; x < worldWidth; x += 1)
                world[x, y] = Convert.ToByte(cellElevations[x, y]);
        return world;
    }

    #endregion

    private static (int[,] cellElevations, int[,] plateIndex) GenerateLandWaterDistribution(
        int worldWidth,
        int worldHeight,
        int jiggle,
        IList<(int, int)> voronoiCells,
        IReadOnlyList<int> landWaterMap)
    {
        // TODO: Refactor into separate water/land field generation function.
        const float scale = .08f;
        var cellElevations = new int[worldWidth, worldHeight];
        var plateIndex = new int[worldWidth, worldHeight];
        var jiggleNoise = Noise.Calc2D(worldWidth, worldHeight, scale);

        for (var y = 0; y < worldHeight; y += 1)
            for (var x = 0; x < worldWidth; x += 1)
            {
                var jiggledX = x + (125.5 - jiggleNoise[x, y]) / 255.0f * jiggle;
                var jiggledY = y + (125.5 - jiggleNoise[x, y]) / 255.0f * jiggle;

                var minDist = double.MaxValue;
                var winnerPlate = 0;
                for (var i = 0; i < voronoiCells.Count; i += 1)
                {
                    var vX = voronoiCells[i].Item1;
                    var vY = voronoiCells[i].Item2;
                    var dist = Math.Sqrt(Math.Pow(vX - jiggledX, 2.0f) + Math.Pow(vY - jiggledY, 2.0f));

                    if (dist < minDist)
                    {
                        minDist = dist;
                        winnerPlate = i;
                    }
                }

                plateIndex[x, y] = winnerPlate;
                cellElevations[x, y] = landWaterMap[winnerPlate];
            }

        return (cellElevations, plateIndex);
    }

    private static bool[] GeneratePlateTypes(int plateCount, Random rng, float landRatio)
    {
        var types = new bool[plateCount];
        for (var i = 0; i < plateCount; i += 1)
            types[i] = rng.NextDouble() < landRatio; // ~55% continental
        return types;
    }

    private static Vector2[] GeneratePlateMotions(int plateCount, Random rng)
    {
        var motions = new Vector2[plateCount];
        for (var i = 0; i < plateCount; i += 1)
        {
            var angle = (float)(rng.NextDouble() * Math.PI * 2.0);
            var speed = (float)(rng.NextDouble() * 0.5 + 0.1);
            motions[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;
        }

        return motions;
    }

    private static float[,] ComputePlateTectonicHeight(
        int worldWidth,
        int worldHeight,
        int[,] plateIndex,
        IList<(int, int)> plateCenters,
        IReadOnlyList<bool> plateTypes,
        IList<Vector2> plateMotions)
    {
        var tectonicDelta = new float[worldWidth, worldHeight];

        for (var y = 0; y < worldHeight; y += 1)
            for (var x = 0; x < worldWidth; x += 1)
            {
                var currentPlate = plateIndex[x, y];
                var totalDelta = 0f;

                (int, int)[] offsets = [(0, -1), (1, 0), (0, 1), (-1, 0)];
                foreach (var (dx, dy) in offsets)
                {
                    var nx = x + dx;
                    var ny = y + dy;
                    if (nx < 0 || nx >= worldWidth || ny < 0 || ny >= worldHeight) continue;

                    var neighbourPlate = plateIndex[nx, ny];
                    if (neighbourPlate == currentPlate) continue;

                    var pA = plateCenters[currentPlate];
                    var pB = plateCenters[neighbourPlate];
                    var direction = new Vector2(pB.Item1 - pA.Item1, pB.Item2 - pA.Item2);
                    if (direction.LengthSquared() < 0.0001f) continue;
                    var normal = Vector2.Normalize(direction);

                    var relMotion = plateMotions[neighbourPlate] - plateMotions[currentPlate];
                    var stress = Vector2.Dot(relMotion, normal);
                    var convergence = MathF.Max(0f, stress);
                    var divergence = MathF.Max(0f, -stress);

                    var continentalInteraction = plateTypes[currentPlate] && plateTypes[neighbourPlate];
                    var mixedInteraction = plateTypes[currentPlate] ^ plateTypes[neighbourPlate];

                    if (convergence > 0)
                    {
                        if (continentalInteraction)
                            totalDelta += convergence * 3.2f;
                        else if (mixedInteraction)
                            totalDelta += convergence * 4.5f;
                        else
                            totalDelta += convergence * 1.7f;
                    }
                    else if (divergence > 0)
                    {
                        totalDelta -= divergence * 2.4f;
                    }
                }

                tectonicDelta[x, y] = totalDelta;
            }

        return tectonicDelta;
    }

    private static float[,] GenerateSlopedCoasts(
        int worldWidth,
        int worldHeight,
        in int[,] cellElevations)
    {
        // 1 Initialize all cells to 9; then set boundary (land adjacent to water) to 0 and enqueue.
        var coastalSlopes = new float[worldWidth, worldHeight];
        for (var y = 0; y < worldHeight; y += 1)
            for (var x = 0; x < worldWidth; x += 1)
                coastalSlopes[x, y] = 9.0f;

        var q = new Queue<(int, int)>(worldWidth * worldHeight);
        for (var y = 1; y < worldHeight - 1; y += 1)
            for (var x = 1; x < worldWidth - 1; x += 1)
            {
                if (cellElevations[x, y] != 4) continue;

                if (cellElevations[x, y - 1] != 3 && // north
                    cellElevations[x + 1, y] != 3 && // east
                    cellElevations[x, y + 1] != 3 && // south
                    cellElevations[x - 1, y] != 3) continue; // west

                coastalSlopes[x, y] = 0.0f;
                q.Enqueue((x, y));
            }

        // 2 BFS: while queue is NOT empty, dequeue cell C and set neighbours to Min(C.elevation + 1, N.elevation)
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

        return coastalSlopes;
    }

    private static float[,] GenerateNoiseMap(
        int mapWidth,
        int mapHeight,
        int seed,
        float scale,
        int octaves,
        float persistance,
        float lacunarity,
        Vector2 offset)
    {
        // For now work with a fixed scale
        const float noiseScale = 0.03f;

        var noiseMap = new float[mapWidth, mapHeight];

        var prng = new Random(seed);
        var octaveOffsets = new Vector2[octaves];
        for (var i = 0; i < octaves; i++)
        {
            var offsetX = prng.Next(-100000, 100000) + offset.X;
            var offsetY = prng.Next(-100000, 100000) + offset.Y;
            octaveOffsets[i] = new Vector2(offsetX, offsetY);
        }

        if (scale <= 0) scale = 0.0001f;

        var maxNoiseHeight = float.MinValue;
        var minNoiseHeight = float.MaxValue;

        for (var y = 0; y < mapHeight; y++)
            for (var x = 0; x < mapWidth; x++)
            {
                float amplitude = 1;
                float frequency = 1;
                float noiseHeight = 0;

                for (var i = 0; i < octaves; i++)
                {
                    var sampleX = Convert.ToInt32(x / scale * frequency + octaveOffsets[i].X);
                    var sampleY = Convert.ToInt32(y / scale * frequency + octaveOffsets[i].Y);
                    var perlinValue = Noise.CalcPixel2D(sampleX, sampleY, noiseScale) * 2 - 1;

                    noiseHeight += perlinValue * amplitude;
                    amplitude *= persistance;
                    frequency *= lacunarity;
                }

                maxNoiseHeight = Math.Max(maxNoiseHeight, noiseHeight);
                minNoiseHeight = Math.Min(minNoiseHeight, noiseHeight);
                noiseMap[x, y] = noiseHeight;
            }

        for (var y = 0; y < mapHeight; y++)
            for (var x = 0; x < mapWidth; x++)
                noiseMap[x, y] = 255f * InverseLerp(minNoiseHeight, maxNoiseHeight, noiseMap[x, y]);

        return noiseMap;
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + t * (b - a);
    }

    private static float InverseLerp(float a, float b, float t)
    {
        return (t - a) / (b - a);
    }
}