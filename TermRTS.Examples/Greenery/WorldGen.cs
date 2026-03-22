using System.Numerics;
using SimplexNoise;

namespace TermRTS.Examples.Greenery;

// Refer to link below for a nice layered noise map implementation:
// https://github.com/SebLague/Procedural-Landmass-Generation/blob/master/Proc%20Gen%20E03/Assets/Scripts/Noise.cs

public enum SurfaceFeature : byte
{
    None = 0,
    River = 1,
    Glacier = 2,
    Lava = 3,
    Mountain = 4,
    Snow = 5,
    Beach = 6,
    Cliff = 7,
    Fjord = 8
}

public class WorldGenerationResult
{
    public WorldGenerationResult(byte[,] elevation, SurfaceFeature[,] surface)
    {
        Elevation = elevation;
        Surface = surface;
    }

    public byte[,] Elevation { get; }
    public SurfaceFeature[,] Surface { get; }
}

public interface IWorldGen
{
    public WorldGenerationResult Generate(int worldWidth, int worldHeight, float landRatio);
}

public class VoronoiWorld(int cellCount, int seed = 0) : IWorldGen
{
    private readonly Random _rng = new(seed);

    // River tuning parameters (adjust at runtime)
    // Lower thresholds create more rivers; higher thresholds make rivers rarer.
    public float RiverFormationThreshold { get; set; } = 0.01f;      // normalized flow-level for river initiation
    public float RiverCarveScale { get; set; } = 3.0f;              // scaling factor for river depth from flow
    public float RiverMaxCarveDepth { get; set; } = 2.0f;           // maximum depth a river can carve

    // Rainfall tuning parameters (used in river generation)
    public int RainfallWaterDistanceRadius { get; set; } = 2;       // search radius to nearest water for rainfall boost
    public float RainfallWaterDistancePenalty { get; set; } = 0.2f; // weight for distance penalty
    public float RainfallMinValue { get; set; } = 0.1f;             // minimum rainfall on land
    public float RainfallElevationDecay { get; set; } = 0.1f;       // how quickly rainfall falls with elevation
    public float RainfallMinModifier { get; set; } = 0.2f;          // minimum modifier due to elevation

    #region IWorldGen Members

    public WorldGenerationResult Generate(int worldWidth, int worldHeight, float landRatio)
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

        // Step 6: Generate hotspots (mantle plumes creating volcanic islands/seamounts)
        var hotspotAdjustment = GenerateHotspots(worldWidth, worldHeight, seed, _rng, cellElevations);

        // Step 7: for each voronoi land cell, apply perlin or simplex noise to generate height
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

        // Step 7: Apply noise and slopes to elevation map.
        for (var y = 0; y < worldHeight; y += 1)
            for (var x = 0; x < worldWidth; x += 1)
            {
                var baseElevation = cellElevations[x, y] == 4 ? 5.0 : -3.0;
                var slopeFactor = coastalSlopes[x, y] / 9.0;
                var normalizedNoise = noiseField[x, y] / 255.0;
                var tectonic = tectonicAdjustment[x, y];
                var hotspot = hotspotAdjustment[x, y];

                var elevation = cellElevations[x, y] + baseElevation * slopeFactor * normalizedNoise + tectonic + hotspot;

                // debug elevation without noise
                //var elevation = cellElevations[x, y] + baseElevation * slopeFactor + tectonic + hotspot;
                // debug: check land-water distribution
                // var elevation = cellElevations[x, y] + baseElevation + tectonic + hotspot;
                // debug: check coastal slope values
                // var elevation = 9 * coastalSlopes[x, y] + tectonic + hotspot;
                cellElevations[x, y] = Convert.ToInt32(Math.Clamp(elevation, 0.0f, 9.0f));
            }

        // Step 8: Apply erosion to smooth terrain and create realistic features
        ApplyErosion(worldWidth, worldHeight, cellElevations);

        // Step 9: Generate rivers based on rainfall and elevation (tunable via public properties)
        var riverMap = GenerateRivers(
            worldWidth,
            worldHeight,
            cellElevations,
            RiverFormationThreshold,
            RiverCarveScale,
            RiverMaxCarveDepth,
            RainfallWaterDistanceRadius,
            RainfallWaterDistancePenalty,
            RainfallMinValue,
            RainfallElevationDecay,
            RainfallMinModifier);

        // Step 10: Apply mountain details (ridges, snow, glacier, lava)
        var surfaceMap = new SurfaceFeature[worldWidth, worldHeight];
        ApplyMountainDetails(worldWidth, worldHeight, cellElevations, surfaceMap, plateIndex, plateTypes, voronoiCells, hotspotAdjustment, riverMap);

        // Step 11: Apply coastal features (beach, cliff, fjord)
        ApplyCoastalFeatures(worldWidth, worldHeight, cellElevations, surfaceMap);

        // optional: apply more techniques from "around the world" to get more appealing shapes

        var world = new byte[worldWidth, worldHeight];
        for (var y = 0; y < worldHeight; y += 1)
            for (var x = 0; x < worldWidth; x += 1)
                world[x, y] = Convert.ToByte(cellElevations[x, y]);

        return new WorldGenerationResult(world, surfaceMap);
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

    private static float[,] GenerateHotspots(int worldWidth, int worldHeight, int seed, Random rng, int[,] cellElevations)
    {
        var hotspotMap = new float[worldWidth, worldHeight];
        var hotspotCount = rng.Next(5, 12); // 5-11 hotspots, more numerous but smaller

        var prng = new Random(seed + 12345); // Different seed for hotspots
        for (var i = 0; i < hotspotCount; i++)
        {
            // Try to place hotspot in water (oceanic) areas only
            var centerX = 0;
            var centerY = 0;
            var attempts = 0;
            const int maxAttempts = 50;

            do
            {
                centerX = prng.Next(worldWidth);
                centerY = prng.Next(worldHeight);
                attempts++;
            } while (cellElevations[centerX, centerY] != 3 && attempts < maxAttempts); // 3 = water

            // If we couldn't find a water location, skip this hotspot
            if (cellElevations[centerX, centerY] != 3) continue;

            var radius = prng.Next(2, 6); // Much smaller radius: 2-5 pixels
            var strength = (float)(prng.NextDouble() * 0.8 + 0.3); // Much smaller strength: 0.3-1.1

            // Create a volcanic cone shape with exponential falloff (more realistic)
            for (var y = Math.Max(0, centerY - radius); y < Math.Min(worldHeight, centerY + radius); y++)
                for (var x = Math.Max(0, centerX - radius); x < Math.Min(worldWidth, centerX + radius); x++)
                {
                    var dx = x - centerX;
                    var dy = y - centerY;
                    var distance = MathF.Sqrt(dx * dx + dy * dy);

                    if (distance > radius) continue;

                    // Exponential falloff for more realistic volcanic shape
                    var normalizedDist = distance / radius;
                    var coneHeight = MathF.Exp(-normalizedDist * 3.0f) * strength;

                    // Add some noise to make it look more volcanic
                    var noise = Noise.CalcPixel2D(x * 20, y * 20, 0.05f) * 0.5f + 0.5f;
                    coneHeight *= (0.7f + noise * 0.6f);

                    hotspotMap[x, y] += coneHeight;
                }
        }

        return hotspotMap;
    }

    private static void ApplyErosion(int worldWidth, int worldHeight, int[,] elevations)
    {
        // Simple thermal erosion + hydraulic erosion simulation
        const int iterations = 5;
        const float talusAngle = 0.5f; // Minimum slope for material to slide
        const float erosionRate = 0.1f;

        for (var iter = 0; iter < iterations; iter++)
        {
            var newElevations = (int[,])elevations.Clone();

            for (var y = 1; y < worldHeight - 1; y++)
                for (var x = 1; x < worldWidth - 1; x++)
                {
                    var currentHeight = elevations[x, y];
                    var lowestNeighbor = currentHeight;
                    var lowestX = x;
                    var lowestY = y;

                    // Find lowest neighbor
                    (int, int)[] directions = [(0, -1), (1, 0), (0, 1), (-1, 0), (1, -1), (1, 1), (-1, 1), (-1, -1)];
                    foreach (var (dx, dy) in directions)
                    {
                        var nx = x + dx;
                        var ny = y + dy;
                        var neighborHeight = elevations[nx, ny];

                        if (neighborHeight < lowestNeighbor)
                        {
                            lowestNeighbor = neighborHeight;
                            lowestX = nx;
                            lowestY = ny;
                        }
                    }

                    var slope = currentHeight - lowestNeighbor;

                    // Thermal erosion: material slides if slope is too steep
                    if (slope > talusAngle)
                    {
                        var slideAmount = Math.Min(erosionRate * slope, slope * 0.5f);
                        newElevations[x, y] -= (int)slideAmount;
                        newElevations[lowestX, lowestY] += (int)slideAmount;
                    }

                    // Hydraulic erosion: water flow simulation (simplified)
                    var waterFlow = Math.Max(0, currentHeight - 0); // Water level at 0
                    if (waterFlow > 0)
                    {
                        var erosionAmount = Math.Min(erosionRate * waterFlow * 0.1f, 0.5f);
                        newElevations[x, y] -= (int)erosionAmount;
                    }
                }

            elevations = newElevations;
        }
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

    private static bool[,] GenerateRivers(
        int worldWidth,
        int worldHeight,
        int[,] elevations,
        float formationThreshold,
        float carveScale,
        float maxCarveDepth,
        int waterDistanceRadius,
        float distancePenalty,
        float rainfallMin,
        float elevationDecay,
        float minModifier)
    {
        // Step 1: Generate rainfall map
        var rainfall = GenerateRainfall(
            worldWidth,
            worldHeight,
            elevations,
            waterDistanceRadius,
            distancePenalty,
            rainfallMin,
            elevationDecay,
            minModifier);

        // Step 2: Calculate flow directions (steepest downhill neighbor)
        var flowDirections = CalculateFlowDirections(worldWidth, worldHeight, elevations);

        // Step 3: Accumulate flow from upstream cells
        var flowAccumulation = AccumulateFlow(worldWidth, worldHeight, flowDirections, rainfall);

        // Step 4: Carve rivers where flow accumulation is high enough
        var riverMap = CarveRivers(worldWidth, worldHeight, elevations, flowAccumulation, formationThreshold, carveScale, maxCarveDepth);

        // Step 5: Deposit sediment in river valleys (optional)
        DepositSediment(worldWidth, worldHeight, elevations, flowAccumulation);

        return riverMap;
    }

    private static float[,] GenerateRainfall(
        int worldWidth,
        int worldHeight,
        int[,] elevations,
        int waterDistanceRadius,
        float distancePenalty,
        float rainfallMin,
        float elevationDecay,
        float minModifier)
    {
        var rainfall = new float[worldWidth, worldHeight];

        // Base rainfall increases with proximity to water and decreases with elevation
        for (var y = 0; y < worldHeight; y++)
            for (var x = 0; x < worldWidth; x++)
            {
                var elevation = elevations[x, y];
                var isWater = elevation <= 1; // Water cells get maximum rainfall

                // Distance to nearest water (simplified - just check neighbors)
                var waterDistance = 0f;
                if (!isWater)
                {
                    var minDistance = float.MaxValue;
                    for (var dy = -waterDistanceRadius; dy <= waterDistanceRadius; dy++)
                        for (var dx = -waterDistanceRadius; dx <= waterDistanceRadius; dx++)
                        {
                            var nx = x + dx;
                            var ny = y + dy;
                            if (nx >= 0 && nx < worldWidth && ny >= 0 && ny < worldHeight)
                            {
                                if (elevations[nx, ny] <= 1)
                                {
                                    var distance = MathF.Sqrt(dx * dx + dy * dy);
                                    minDistance = MathF.Min(minDistance, distance);
                                }
                            }
                        }
                    waterDistance = minDistance == float.MaxValue ? waterDistanceRadius : minDistance;
                }

                // Rainfall formula: high near water, decreases with elevation and distance
                var baseRainfall = isWater ? 1.0f : MathF.Max(rainfallMin, 1.0f - waterDistance * distancePenalty);
                var elevationModifier = MathF.Max(minModifier, 1.0f - elevation * elevationDecay);
                rainfall[x, y] = baseRainfall * elevationModifier;
            }

        return rainfall;
    }

    private static (int, int)[,] CalculateFlowDirections(int worldWidth, int worldHeight, int[,] elevations)
    {
        var flowDirections = new (int, int)[worldWidth, worldHeight];

        for (var y = 0; y < worldHeight; y++)
            for (var x = 0; x < worldWidth; x++)
            {
                var currentElevation = elevations[x, y];
                var steepestDrop = 0f;
                var bestDirection = (0, 0);

                // Check all 8 neighbors
                for (var dy = -1; dy <= 1; dy++)
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        var nx = x + dx;
                        var ny = y + dy;

                        if (nx >= 0 && nx < worldWidth && ny >= 0 && ny < worldHeight)
                        {
                            var neighborElevation = elevations[nx, ny];
                            var drop = currentElevation - neighborElevation;

                            if (drop > steepestDrop)
                            {
                                steepestDrop = drop;
                                bestDirection = (dx, dy);
                            }
                        }
                    }

                flowDirections[x, y] = bestDirection;
            }

        return flowDirections;
    }

    private static float[,] AccumulateFlow(int worldWidth, int worldHeight, (int, int)[,] flowDirections, float[,] rainfall)
    {
        var flowAccumulation = new float[worldWidth, worldHeight];

        // Initialize with rainfall
        for (var y = 0; y < worldHeight; y++)
            for (var x = 0; x < worldWidth; x++)
                flowAccumulation[x, y] = rainfall[x, y];

        // Propagate flow downstream (process in reverse order to handle dependencies)
        var processed = new bool[worldWidth, worldHeight];
        var queue = new Queue<(int, int)>();

        // Start with cells that have no incoming flow (local minima or edges)
        for (var y = 0; y < worldHeight; y++)
            for (var x = 0; x < worldWidth; x++)
            {
                var hasIncoming = false;
                for (var dy = -1; dy <= 1 && !hasIncoming; dy++)
                    for (var dx = -1; dx <= 1 && !hasIncoming; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        var nx = x + dx;
                        var ny = y + dy;
                        if (nx >= 0 && nx < worldWidth && ny >= 0 && ny < worldHeight)
                        {
                            var (fdx, fdy) = flowDirections[nx, ny];
                            if (nx + fdx == x && ny + fdy == y)
                            {
                                hasIncoming = true;
                            }
                        }
                    }

                if (!hasIncoming)
                {
                    queue.Enqueue((x, y));
                    processed[x, y] = true;
                }
            }

        // Process queue
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            var (dx, dy) = flowDirections[x, y];

            if (dx != 0 || dy != 0)
            {
                var nx = x + dx;
                var ny = y + dy;

                if (nx >= 0 && nx < worldWidth && ny >= 0 && ny < worldHeight)
                {
                    flowAccumulation[nx, ny] += flowAccumulation[x, y];

                    // Check if all upstream cells are processed
                    var allUpstreamProcessed = true;
                    for (var dy2 = -1; dy2 <= 1 && allUpstreamProcessed; dy2++)
                        for (var dx2 = -1; dx2 <= 1 && allUpstreamProcessed; dx2++)
                        {
                            if (dx2 == 0 && dy2 == 0) continue;
                            var nx2 = nx + dx2;
                            var ny2 = ny + dy2;
                            if (nx2 >= 0 && nx2 < worldWidth && ny2 >= 0 && ny2 < worldHeight)
                            {
                                var (fdx2, fdy2) = flowDirections[nx2, ny2];
                                if (nx2 + fdx2 == nx && ny2 + fdy2 == ny && !processed[nx2, ny2])
                                {
                                    allUpstreamProcessed = false;
                                }
                            }
                        }

                    if (allUpstreamProcessed && !processed[nx, ny])
                    {
                        processed[nx, ny] = true;
                        queue.Enqueue((nx, ny));
                    }
                }
            }
        }

        return flowAccumulation;
    }

    private static bool[,] CarveRivers(
        int worldWidth,
        int worldHeight,
        int[,] elevations,
        float[,] flowAccumulation,
        float formationThreshold,
        float carveScale,
        float maxCarveDepth)
    {
        var riverMap = new bool[worldWidth, worldHeight];

        // Find maximum flow accumulation for normalization
        var maxFlow = 0f;
        for (var y = 0; y < worldHeight; y++)
            for (var x = 0; x < worldWidth; x++)
                maxFlow = MathF.Max(maxFlow, flowAccumulation[x, y]);

        // Carve rivers where flow accumulation is high enough
        for (var y = 0; y < worldHeight; y++)
            for (var x = 0; x < worldWidth; x++)
            {
                var normalizedFlow = maxFlow > 0 ? flowAccumulation[x, y] / maxFlow : 0f;
                var currentElevation = elevations[x, y];

                // Only carve on land, not in water
                if (currentElevation >= 4)
                {
                    // Rivers form where flow accumulation is significant
                    if (normalizedFlow > formationThreshold) // Threshold for river formation
                    {
                        riverMap[x, y] = true;

                        // Carve depth based on flow amount (more flow = deeper river)
                        var carveDepth = MathF.Min(maxCarveDepth, normalizedFlow * carveScale);
                        elevations[x, y] = Math.Max(3, currentElevation - (int)carveDepth);
                    }
                }
            }

        return riverMap;
    }

    private static void DepositSediment(int worldWidth, int worldHeight, int[,] elevations, float[,] flowAccumulation)
    {
        // Simple sediment deposition in low areas
        for (var y = 0; y < worldHeight; y++)
            for (var x = 0; x < worldWidth; x++)
            {
                var currentElevation = elevations[x, y];

                // Deposit sediment in very low areas (potential floodplains)
                if (currentElevation <= 2 && flowAccumulation[x, y] > 0)
                {
                    // Small sediment deposit
                    elevations[x, y] = Math.Min(9, currentElevation + 1);
                }
            }
    }

    private static void ApplyMountainDetails(
        int worldWidth,
        int worldHeight,
        int[,] elevations,
        SurfaceFeature[,] surfaceMap,
        int[,] plateIndex,
        bool[] plateTypes,
        IList<(int, int)> plateCenters,
        float[,] hotspotMap,
        bool[,] riverMap)
    {
        for (var y = 0; y < worldHeight; y++)
            for (var x = 0; x < worldWidth; x++)
            {
                surfaceMap[x, y] = SurfaceFeature.None;

                var elevation = elevations[x, y];
                if (elevation >= 7)
                    surfaceMap[x, y] = SurfaceFeature.Mountain;

                if (elevation >= 8)
                    surfaceMap[x, y] = SurfaceFeature.Snow;

                if (elevation == 9 && !riverMap[x, y])
                    surfaceMap[x, y] = SurfaceFeature.Glacier;

                if (riverMap[x, y])
                    surfaceMap[x, y] = SurfaceFeature.River;

                if (hotspotMap[x, y] > 0.7f && elevation >= 5)
                    surfaceMap[x, y] = SurfaceFeature.Lava;
            }

        // Plate boundary mountain ridges: continental collisions
        (int, int)[] neighbors = new (int, int)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        for (var y = 0; y < worldHeight; y++)
            for (var x = 0; x < worldWidth; x++)
            {
                var currentPlate = plateIndex[x, y];
                if (currentPlate < 0 || currentPlate >= plateTypes.Length) continue;

                foreach (var (dx, dy) in neighbors)
                {
                    var nx = x + dx;
                    var ny = y + dy;

                    if (nx < 0 || nx >= worldWidth || ny < 0 || ny >= worldHeight) continue;

                    var neighborPlate = plateIndex[nx, ny];
                    if (neighborPlate == currentPlate) continue;

                    if (neighborPlate < 0 || neighborPlate >= plateTypes.Length) continue;

                                    surfaceMap[x, y] = SurfaceFeature.Mountain;
                }
            }
    }

    private static void ApplyCoastalFeatures(
        int worldWidth,
        int worldHeight,
        int[,] elevations,
        SurfaceFeature[,] surfaceMap)
    {
        for (var y = 0; y < worldHeight; y++)
            for (var x = 0; x < worldWidth; x++)
            {
                // Skip existing assigned strong surface features: river, lava, glacier
                var existing = surfaceMap[x, y];
                if (existing == SurfaceFeature.River || existing == SurfaceFeature.Lava || existing == SurfaceFeature.Glacier)
                    continue;

                var elevation = elevations[x, y];
                var isWater = elevation <= 3;

                // Beach/cliff only for land cells near water
                if (!isWater)
                {
                    var adjacentWater = 0;
                    var maxAdjElevation = 0;
                    for (var dy = -1; dy <= 1; dy++)
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            var nx = x + dx;
                            var ny = y + dy;
                            if (nx < 0 || nx >= worldWidth || ny < 0 || ny >= worldHeight) continue;
                            var neighborElevation = elevations[nx, ny];
                            if (neighborElevation <= 3)
                                adjacentWater++;
                            else
                                maxAdjElevation = Math.Max(maxAdjElevation, neighborElevation);
                        }

                    if (adjacentWater > 0)
                    {
                        // steep cliff if high land adjacent to water and steep drops nearby
                        if (elevation >= 7 && maxAdjElevation <= 3)
                        {
                            surfaceMap[x, y] = SurfaceFeature.Cliff;
                        }
                        else if (elevation <= 6)
                        {
                            surfaceMap[x, y] = SurfaceFeature.Beach;
                        }
                        else
                        {
                            // somewhat high coast at moderate slope -> cliff
                            surfaceMap[x, y] = SurfaceFeature.Cliff;
                        }
                    }
                }
                else
                {
                    // water cells: detect fjord (narrow water in high mountains)
                    var adjacentLand = 0;
                    var adjacentHighMountain = 0;
                    for (var dy = -1; dy <= 1; dy++)
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            var nx = x + dx;
                            var ny = y + dy;
                            if (nx < 0 || nx >= worldWidth || ny < 0 || ny >= worldHeight) continue;
                            var neighborElevation = elevations[nx, ny];
                            if (neighborElevation >= 4)
                            {
                                adjacentLand++;
                                if (neighborElevation >= 8)
                                    adjacentHighMountain++;
                            }
                        }

                    if (adjacentLand >= 3 && adjacentHighMountain >= 1)
                    {
                        surfaceMap[x, y] = SurfaceFeature.Fjord;
                    }
                }
            }
    }
}