using System.Numerics;
using System.Collections.Generic;
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
    Fjord = 8,
    Crater = 9,
    Ash = 10,
    Cinder = 11,
    Caldera = 12,
    Shield = 13,
    Stratovolcano = 14
}

public enum Biome : byte
{
    Ocean = 0,
    Tundra = 1,
    Taiga = 2,
    TemperateForest = 3,
    Grassland = 4,
    Desert = 5,
    TropicalForest = 6,
    Savanna = 7,
    IceCap = 8
}

public class WorldGenerationResult
{
    public WorldGenerationResult(byte[,] elevation, SurfaceFeature[,] surface, float[,] temperature, float[,] humidity, Biome[,] biomes, float[,] temperatureAmplitude)
    {
        Elevation = elevation;
        Surface = surface;
        Temperature = temperature;
        Humidity = humidity;
        Biomes = biomes;
        TemperatureAmplitude = temperatureAmplitude;
    }

    public byte[,] Elevation { get; }
    public SurfaceFeature[,] Surface { get; }
    public float[,] Temperature { get; }
    public float[,] Humidity { get; }
    public Biome[,] Biomes { get; }
    public float[,] TemperatureAmplitude { get; }
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

    // Island chain (hotspot) tuning parameters
    public int MinIslandChains { get; set; } = 3;                   // minimum number of island chains
    public int MaxIslandChains { get; set; } = 7;                   // maximum number of island chains
    public int MinChainLength { get; set; } = 3;                    // minimum hotspots per chain
    public int MaxChainLength { get; set; } = 8;                    // maximum hotspots per chain
    public int ChainSpacing { get; set; } = 25;                     // spacing between hotspots in a chain
    public int MinLandDistance { get; set; } = 8;                   // minimum distance from land for hotspot placement
    public int MinHotspotRadius { get; set; } = 3;                  // minimum radius of volcanic cones
    public int MaxHotspotRadius { get; set; } = 8;                  // maximum radius of volcanic cones
    public float MinHotspotStrength { get; set; } = 0.4f;           // minimum elevation strength of hotspots
    public float MaxHotspotStrength { get; set; } = 1.1f;           // maximum elevation strength of hotspots

    // Advanced erosion tuning parameters
    public bool UseAdvancedErosion { get; set; } = true;             // enable advanced erosion system
    public int ErosionIterations { get; set; } = 10;                 // number of erosion iterations (further reduced)
    public float HydraulicErosionRate { get; set; } = 0.001f;        // base hydraulic erosion rate (much more gentle)
    public float SedimentCapacity { get; set; } = 0.01f;             // maximum sediment a water cell can carry (much lower)
    public float DepositionRate { get; set; } = 0.001f;              // rate at which sediment is deposited (much slower)
    public float EvaporationRate { get; set; } = 0.001f;             // water evaporation rate
    public float RainRate { get; set; } = 0.00005f;                  // rainfall rate (much less water)
    public float ThermalErosionRate { get; set; } = 0.05f;           // thermal erosion rate (very gentle)
    public float TalusAngle { get; set; } = 0.5f;                    // minimum slope for material to slide
    public float MinSlope { get; set; } = 0.01f;                     // minimum slope for water flow
    public float Gravity { get; set; } = 9.81f;                      // gravity constant for water flow
    public float WaterViscosity { get; set; } = 0.001f;              // water viscosity for flow calculations

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
            landWaterMap[i] = plateTypes[i] ? 4 : 1; // Lower oceanic plates to create deeper oceans

        // step 3: associate each grid cell to one of the voronoi cells
        const int jiggle = 15;
        var (cellElevationsInt, plateIndex) = GenerateLandWaterDistribution(
            worldWidth, worldHeight, jiggle, voronoiCells, landWaterMap);
        
        // Convert to float for more precise calculations
        var cellElevations = new float[worldWidth, worldHeight];
        for (var y = 0; y < worldHeight; y++)
            for (var x = 0; x < worldWidth; x++)
                cellElevations[x, y] = cellElevationsInt[x, y];

        // Step 4: Generate coastal slopes for each voronoi cell.
        var coastalSlopes = GenerateSlopedCoasts(worldWidth, worldHeight, cellElevationsInt);

        // Step 5: Compute plate tectonics influence (mountains/trenches along plate boundaries)
        var tectonicAdjustment = ComputePlateTectonicHeight(
            worldWidth,
            worldHeight,
            plateIndex,
            voronoiCells,
            plateTypes,
            plateMotions);

        // Step 6: Generate hotspots (mantle plumes creating volcanic islands/seamounts)
        var hotspotAdjustment = GenerateHotspots(worldWidth, worldHeight, seed, _rng, cellElevationsInt, plateMotions,
            MinIslandChains, MaxIslandChains, MinChainLength, MaxChainLength, ChainSpacing, MinLandDistance,
            MinHotspotRadius, MaxHotspotRadius, MinHotspotStrength, MaxHotspotStrength);

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
                var baseElevation = cellElevationsInt[x, y] >= 3 ? 5.0 : -5.0; // Continental plates (>=3) get higher base, oceanic (<3) get much lower base for deep trenches
                var slopeFactor = coastalSlopes[x, y] / 9.0;
                var normalizedNoise = noiseField[x, y] / 255.0;
                var tectonic = tectonicAdjustment[x, y];
                var hotspot = hotspotAdjustment[x, y];

                // For oceanic plates, reduce noise impact to allow deeper trenches
                var cellElevationContribution = cellElevationsInt[x, y] >= 3 ? cellElevationsInt[x, y] : 0;
                var noiseMultiplier = cellElevationsInt[x, y] >= 3 ? 1.0f : 0.3f; // Less noise in oceans
                var elevation = cellElevationContribution + baseElevation * slopeFactor * normalizedNoise * noiseMultiplier + tectonic + hotspot;

                // Store as float, don't clamp yet
                cellElevations[x, y] = (float)elevation;
            }

        // Step 7: Generate climate (temperature, humidity, biomes, seasonal effects) - moved before erosion
        var (temperature, humidity, biomes, temperatureAmplitude) = GenerateClimate(worldWidth, worldHeight, cellElevationsInt);

        // Step 8: Apply erosion to smooth terrain and create realistic features
        if (UseAdvancedErosion)
        {
            ApplyAdvancedErosion(worldWidth, worldHeight, cellElevations, hotspotAdjustment,
                ErosionIterations, HydraulicErosionRate, SedimentCapacity, DepositionRate,
                EvaporationRate, RainRate, ThermalErosionRate, TalusAngle, MinSlope, Gravity, WaterViscosity, biomes, humidity);
        }
        else
        {
            ApplyErosion(worldWidth, worldHeight, cellElevations);
        }

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

        // Convert to final integer elevations, allowing negative values to become 0 (deep trenches)
        var finalElevations = new int[worldWidth, worldHeight];
        for (var y = 0; y < worldHeight; y += 1)
            for (var x = 0; x < worldWidth; x += 1)
                finalElevations[x, y] = Math.Clamp((int)Math.Round(cellElevations[x, y]), 0, 9);

        var world = new byte[worldWidth, worldHeight];
        for (var y = 0; y < worldHeight; y += 1)
            for (var x = 0; x < worldWidth; x += 1)
                world[x, y] = Convert.ToByte(finalElevations[x, y]);

        return new WorldGenerationResult(world, surfaceMap, temperature, humidity, biomes, temperatureAmplitude);
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
                        if (!plateTypes[currentPlate] && !plateTypes[neighbourPlate])
                            totalDelta -= divergence * 5.0f; // Much deeper trenches for oceanic-oceanic divergence
                        else
                            totalDelta -= divergence * 2.4f;
                    }
                }

                tectonicDelta[x, y] = totalDelta;
            }

        return tectonicDelta;
    }

    private static float[,] GenerateHotspots(int worldWidth, int worldHeight, int seed, Random rng, int[,] cellElevations, IList<Vector2> plateMotions,
        int minIslandChains, int maxIslandChains, int minChainLength, int maxChainLength, int chainSpacing, int minLandDistance,
        int minHotspotRadius, int maxHotspotRadius, float minHotspotStrength, float maxHotspotStrength)
    {
        var hotspotMap = new float[worldWidth, worldHeight];
        var chainCount = rng.Next(minIslandChains, maxIslandChains + 1); // +1 because Next upper bound is exclusive

        var prng = new Random(seed + 12345); // Different seed for hotspots

        for (var chain = 0; chain < chainCount; chain++)
        {
            // Create a chain of hotspots along a plate motion direction
            var chainLength = prng.Next(minChainLength, maxChainLength + 1);
            var startX = prng.Next(worldWidth);
            var startY = prng.Next(worldHeight);

            // Use a random plate motion direction for the chain, or create a random direction
            var chainDirection = plateMotions.Count > 0
                ? plateMotions[prng.Next(plateMotions.Count)]
                : new Vector2((float)(prng.NextDouble() - 0.5) * 2, (float)(prng.NextDouble() - 0.5) * 2);

            // Normalize and scale the direction
            var length = MathF.Sqrt(chainDirection.X * chainDirection.X + chainDirection.Y * chainDirection.Y);
            if (length > 0)
            {
                chainDirection = chainDirection / length * (float)(prng.NextDouble() * 2 + 1);
            }

            for (var i = 0; i < chainLength; i++)
            {
                // Calculate position along the chain with configurable spacing
                var offsetX = (int)(chainDirection.X * i * chainSpacing);
                var offsetY = (int)(chainDirection.Y * i * chainSpacing);
                var centerX = startX + offsetX;
                var centerY = startY + offsetY;

                // Keep within bounds with some wrapping
                centerX = ((centerX % worldWidth) + worldWidth) % worldWidth;
                centerY = ((centerY % worldHeight) + worldHeight) % worldHeight;

                // Try to place hotspot in deep ocean areas, far from land
                var attempts = 0;
                const int maxAttempts = 30;
                var placed = false;

                while (attempts < maxAttempts && !placed)
                {
                    var testX = centerX + prng.Next(-15, 16); // Wider search area
                    var testY = centerY + prng.Next(-15, 16);

                    testX = ((testX % worldWidth) + worldWidth) % worldWidth;
                    testY = ((testY % worldHeight) + worldHeight) % worldHeight;

                    // Check if this is deep ocean (elevation 0-2) and far from land
                    if (cellElevations[testX, testY] <= 2 && IsFarFromLand(testX, testY, worldWidth, worldHeight, cellElevations, minLandDistance))
                    {
                        centerX = testX;
                        centerY = testY;
                        placed = true;
                    }
                    attempts++;
                }

                if (!placed) continue; // Skip this hotspot if we couldn't place it in deep ocean

                var radius = prng.Next(minHotspotRadius, maxHotspotRadius + 1);
                var strength = (float)(prng.NextDouble() * (maxHotspotStrength - minHotspotStrength) + minHotspotStrength);

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
        }

        return hotspotMap;
    }

    private static bool IsFarFromLand(int x, int y, int worldWidth, int worldHeight, int[,] elevations, int minDistance)
    {
        // Check a radius around the point to ensure no land is nearby
        for (var dy = -minDistance; dy <= minDistance; dy++)
        {
            for (var dx = -minDistance; dx <= minDistance; dx++)
            {
                var checkX = x + dx;
                var checkY = y + dy;

                // Wrap around world edges
                checkX = ((checkX % worldWidth) + worldWidth) % worldWidth;
                checkY = ((checkY % worldHeight) + worldHeight) % worldHeight;

                if (elevations[checkX, checkY] >= 4) // Land
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static void ApplyErosion(int worldWidth, int worldHeight, float[,] elevations)
    {
        // Simple thermal erosion + hydraulic erosion simulation
        const int iterations = 5;
        const float talusAngle = 0.5f; // Minimum slope for material to slide
        const float erosionRate = 0.1f;

        for (var iter = 0; iter < iterations; iter++)
        {
            var newElevations = (float[,])elevations.Clone();

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
                        newElevations[x, y] -= slideAmount;
                        newElevations[lowestX, lowestY] += slideAmount;
                    }

                    // Hydraulic erosion: water flow simulation (simplified)
                    var waterFlow = Math.Max(0, currentHeight - 0); // Water level at 0
                    if (waterFlow > 0)
                    {
                        var erosionAmount = Math.Min(erosionRate * waterFlow * 0.1f, 0.5f);
                        newElevations[x, y] -= erosionAmount;
                    }
                }

            elevations = newElevations;
        }
    }

    private static void ApplyAdvancedErosion(int worldWidth, int worldHeight, float[,] elevations, float[,] hotspotMap,
        int iterations, float hydraulicErosionRate, float sedimentCapacity, float depositionRate,
        float evaporationRate, float rainRate, float thermalErosionRate, float talusAngle,
        float minSlope, float gravity, float waterViscosity, Biome[,] biomes, float[,] humidity)
    {
        // Advanced erosion with hydraulic simulation, sediment transport, and thermal erosion
        var water = new float[worldWidth, worldHeight];
        var sediment = new float[worldWidth, worldHeight];
        var terrain = new float[worldWidth, worldHeight];

        // Copy elevations to terrain (already float)
        for (var y = 0; y < worldHeight; y++)
            for (var x = 0; x < worldWidth; x++)
                terrain[x, y] = elevations[x, y];

        for (var iter = 0; iter < iterations; iter++)
        {
            // Step 1: Add rainfall (climate-aware)
            for (var y = 0; y < worldHeight; y++)
                for (var x = 0; x < worldWidth; x++)
                {
                    var localRainRate = rainRate;
                    // Drier regions get less rainfall
                    if (biomes[x, y] == Biome.Desert || biomes[x, y] == Biome.Tundra)
                        localRainRate *= 0.3f; // 70% less rain in dry/arid regions
                    else if (humidity[x, y] < 0.3f)
                        localRainRate *= 0.5f; // 50% less rain in dry areas
                    
                    water[x, y] += localRainRate;
                }

            // Step 2: Water flow simulation
            var velocityX = new float[worldWidth, worldHeight];
            var velocityY = new float[worldWidth, worldHeight];

            // Calculate water flow directions and velocities
            for (var y = 1; y < worldHeight - 1; y++)
                for (var x = 1; x < worldWidth - 1; x++)
                {
                    // Calculate slope in all directions
                    var slopeX = (terrain[x - 1, y] + terrain[x + 1, y]) * 0.5f - terrain[x, y];
                    var slopeY = (terrain[x, y - 1] + terrain[x, y + 1]) * 0.5f - terrain[x, y];

                    var slope = MathF.Sqrt(slopeX * slopeX + slopeY * slopeY);
                    if (slope < minSlope) continue;

                    // Calculate flow direction
                    var flowX = slopeX / slope;
                    var flowY = slopeY / slope;

                    // Calculate velocity based on slope and water depth
                    var velocity = MathF.Sqrt(gravity * slope) * water[x, y];
                    velocityX[x, y] = flowX * velocity;
                    velocityY[x, y] = flowY * velocity;
                }

            // Step 3: Hydraulic erosion and sediment transport
            var newTerrain = (float[,])terrain.Clone();
            var newWater = (float[,])water.Clone();
            var newSediment = (float[,])sediment.Clone();

            for (var y = 1; y < worldHeight - 1; y++)
                for (var x = 1; x < worldWidth - 1; x++)
                {
                    var currentWater = water[x, y];
                    if (currentWater <= 0) continue;

                    // Calculate sediment capacity based on water velocity
                    var velocity = MathF.Sqrt(velocityX[x, y] * velocityX[x, y] + velocityY[x, y] * velocityY[x, y]);
                    var capacity = sedimentCapacity * velocity * currentWater;

                    var currentSediment = sediment[x, y];

                    if (currentSediment > capacity)
                    {
                        // Deposit sediment
                        var depositAmount = (currentSediment - capacity) * depositionRate;
                        newTerrain[x, y] += depositAmount;
                        newSediment[x, y] -= depositAmount;
                    }
                    else
                    {
                        // Erode terrain (with volcanic resistance)
                        var erodeAmount = (capacity - currentSediment) * hydraulicErosionRate;
                        
                        // Volcanic areas are much more resistant to erosion
                        var volcanicResistance = 1.0f;
                        if (hotspotMap[x, y] > 0.1f) // Areas with volcanic activity
                        {
                            volcanicResistance = 0.1f; // 90% less erosion in volcanic areas
                        }
                        
                        erodeAmount *= volcanicResistance;
                        erodeAmount = Math.Min(erodeAmount, terrain[x, y] * 0.1f); // Don't erode too much
                        newTerrain[x, y] -= erodeAmount;
                        newSediment[x, y] += erodeAmount;
                    }

                    // Water flow to neighboring cells
                    var totalOutflow = 0f;
                    (int, int)[] directions = [(0, -1), (1, 0), (0, 1), (-1, 0)];

                    foreach (var (dx, dy) in directions)
                    {
                        var nx = x + dx;
                        var ny = y + dy;

                        if (nx < 0 || nx >= worldWidth || ny < 0 || ny >= worldHeight) continue;

                        var neighborSlope = terrain[x, y] - terrain[nx, ny];
                        if (neighborSlope > 0)
                        {
                            var outflow = currentWater * neighborSlope * hydraulicErosionRate;
                            newWater[nx, ny] += outflow * 0.25f; // Distribute to neighbors
                            newSediment[nx, ny] += currentSediment * outflow / Math.Max(currentWater, 0.001f) * 0.25f;
                            totalOutflow += outflow;
                        }
                    }

                    newWater[x, y] -= totalOutflow;
                    newSediment[x, y] -= currentSediment * totalOutflow / Math.Max(currentWater, 0.001f);
                }

            // Step 4: Thermal erosion (material sliding)
            for (var y = 1; y < worldHeight - 1; y++)
                for (var x = 1; x < worldWidth - 1; x++)
                {
                    var currentHeight = newTerrain[x, y];
                    var lowestNeighbor = currentHeight;
                    var lowestX = x;
                    var lowestY = y;

                    // Find lowest neighbor
                    (int, int)[] directions = [(0, -1), (1, 0), (0, 1), (-1, 0), (1, -1), (1, 1), (-1, 1), (-1, -1)];
                    foreach (var (dx, dy) in directions)
                    {
                        var nx = x + dx;
                        var ny = y + dy;
                        var neighborHeight = newTerrain[nx, ny];

                        if (neighborHeight < lowestNeighbor)
                        {
                            lowestNeighbor = neighborHeight;
                            lowestX = nx;
                            lowestY = ny;
                        }
                    }

                    var slope = currentHeight - lowestNeighbor;

                    // Material slides if slope is too steep
                    if (slope > talusAngle)
                    {
                        var slideAmount = Math.Min(thermalErosionRate * slope, slope * 0.5f);
                        newTerrain[x, y] -= slideAmount;
                        newTerrain[lowestX, lowestY] += slideAmount;
                    }
                }

            // Step 5: Evaporation (climate-aware)
            for (var y = 0; y < worldHeight; y++)
                for (var x = 0; x < worldWidth; x++)
                {
                    var baseEvaporation = evaporationRate;
                    
                    // Higher evaporation in dry/hot climates
                    var climateModifier = 1.0f;
                    var biome = biomes[x, y];
                    var humidityValue = humidity[x, y];
                    
                    if (biome == Biome.Desert || biome == Biome.Tundra)
                        climateModifier = 2.0f; // 2x evaporation in dry areas
                    else if (humidityValue < 0.3f)
                        climateModifier = 1.5f; // 1.5x evaporation in low humidity areas
                    
                    var adjustedEvaporation = baseEvaporation * climateModifier;
                    newWater[x, y] = Math.Max(0, newWater[x, y] - adjustedEvaporation);
                }

            // Update arrays
            terrain = newTerrain;
            water = newWater;
            sediment = newSediment;
        }

        // Copy back to elevations (keep as float for now)
        for (var y = 0; y < worldHeight; y++)
            for (var x = 0; x < worldWidth; x++)
                elevations[x, y] = terrain[x, y];
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
        float[,] elevations,
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
        float[,] elevations,
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

    private static (int, int)[,] CalculateFlowDirections(int worldWidth, int worldHeight, float[,] elevations)
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
        float[,] elevations,
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

    private static void DepositSediment(int worldWidth, int worldHeight, float[,] elevations, float[,] flowAccumulation)
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
        float[,] elevations,
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

        // Add volcanic details around hotspots
        var hotspotCenters = FindHotspotCenters(worldWidth, worldHeight, hotspotMap);
        foreach (var (centerX, centerY, strength) in hotspotCenters)
        {
            AddVolcanicDetails(worldWidth, worldHeight, elevations, surfaceMap, centerX, centerY, strength);
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

    private static List<(int x, int y, float strength)> FindHotspotCenters(int worldWidth, int worldHeight, float[,] hotspotMap)
    {
        var centers = new List<(int, int, float)>();
        var visited = new bool[worldWidth, worldHeight];

        for (var y = 0; y < worldHeight; y++)
        {
            for (var x = 0; x < worldWidth; x++)
            {
                if (visited[x, y] || hotspotMap[x, y] < 0.3f) continue;

                // Find local maximum
                var maxStrength = hotspotMap[x, y];
                var maxX = x;
                var maxY = y;

                // Search in a small radius for the actual peak
                for (var dy = -3; dy <= 3; dy++)
                {
                    for (var dx = -3; dx <= 3; dx++)
                    {
                        var nx = x + dx;
                        var ny = y + dy;
                        if (nx < 0 || nx >= worldWidth || ny < 0 || ny >= worldHeight) continue;

                        if (hotspotMap[nx, ny] > maxStrength)
                        {
                            maxStrength = hotspotMap[nx, ny];
                            maxX = nx;
                            maxY = ny;
                        }
                    }
                }

                // Mark area around peak as visited
                for (var dy = -5; dy <= 5; dy++)
                {
                    for (var dx = -5; dx <= 5; dx++)
                    {
                        var nx = maxX + dx;
                        var ny = maxY + dy;
                        if (nx >= 0 && nx < worldWidth && ny >= 0 && ny < worldHeight)
                        {
                            visited[nx, ny] = true;
                        }
                    }
                }

                centers.Add((maxX, maxY, maxStrength));
            }
        }

        return centers;
    }

    private static void AddVolcanicDetails(int worldWidth, int worldHeight, float[,] elevations, SurfaceFeature[,] surfaceMap, int centerX, int centerY, float strength)
    {
        var radius = Math.Max(3, (int)(strength * 10)); // Scale radius with strength, minimum 3

        // Determine volcano type based on strength and characteristics
        SurfaceFeature volcanoType;
        if (strength > 0.9f)
        {
            volcanoType = SurfaceFeature.Stratovolcano; // Tall, steep volcanoes
        }
        else if (strength > 0.6f)
        {
            volcanoType = SurfaceFeature.Shield; // Broad, flat volcanoes
        }
        else
        {
            volcanoType = SurfaceFeature.Cinder; // Small cinder cones
        }

        // Add crater at the peak for stratovolcanoes and large shields
        if (centerX >= 0 && centerX < worldWidth && centerY >= 0 && centerY < worldHeight)
        {
            var elevation = elevations[centerX, centerY];
            if (elevation >= 6 && (volcanoType == SurfaceFeature.Stratovolcano || (volcanoType == SurfaceFeature.Shield && strength > 0.8f)))
            {
                surfaceMap[centerX, centerY] = SurfaceFeature.Crater;
            }
            else if (elevation >= 5 && volcanoType == SurfaceFeature.Cinder)
            {
                surfaceMap[centerX, centerY] = SurfaceFeature.Cinder;
            }
        }

        // Add volcanic features based on volcano type
        for (var y = Math.Max(0, centerY - radius); y < Math.Min(worldHeight, centerY + radius); y++)
        {
            for (var x = Math.Max(0, centerX - radius); x < Math.Min(worldWidth, centerX + radius); x++)
            {
                var dx = x - centerX;
                var dy = y - centerY;
                var distance = MathF.Sqrt(dx * dx + dy * dy);

                if (distance > radius) continue;

                var normalizedDist = distance / radius;
                var elevation = elevations[x, y];

                // Skip if already has strong features
                var existing = surfaceMap[x, y];
                if (existing == SurfaceFeature.River || existing == SurfaceFeature.Glacier || existing == SurfaceFeature.Lava)
                    continue;

                if (volcanoType == SurfaceFeature.Stratovolcano)
                {
                    // Stratovolcanoes: steep, explosive, with ash and lava
                    if (normalizedDist < 0.4f && elevation >= 7)
                    {
                        surfaceMap[x, y] = SurfaceFeature.Stratovolcano;
                    }
                    else if (normalizedDist > 0.3f && normalizedDist < 0.8f && elevation >= 5)
                    {
                        surfaceMap[x, y] = SurfaceFeature.Ash;
                    }
                }
                else if (volcanoType == SurfaceFeature.Shield)
                {
                    // Shield volcanoes: broad, gentle slopes, mostly lava
                    if (normalizedDist < 0.6f && elevation >= 5)
                    {
                        surfaceMap[x, y] = SurfaceFeature.Shield;
                    }
                    else if (normalizedDist > 0.5f && elevation >= 4)
                    {
                        surfaceMap[x, y] = SurfaceFeature.Lava;
                    }
                }
                else // Cinder
                {
                    // Cinder cones: small, steep, cinder and ash
                    if (normalizedDist < 0.5f && elevation >= 4)
                    {
                        surfaceMap[x, y] = SurfaceFeature.Cinder;
                    }
                    else if (normalizedDist > 0.4f && elevation >= 3)
                    {
                        surfaceMap[x, y] = SurfaceFeature.Ash;
                    }
                }

                // Add caldera for large stratovolcanoes at the center
                if (normalizedDist < 0.2f && volcanoType == SurfaceFeature.Stratovolcano && strength > 0.9f && elevation >= 7)
                {
                    surfaceMap[x, y] = SurfaceFeature.Caldera;
                }
            }
        }
    }

    private static void ApplyCoastalFeatures(
        int worldWidth,
        int worldHeight,
        float[,] elevations,
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
                    var maxAdjElevation = 0f;
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

    private static (float[,], float[,], Biome[,], float[,]) GenerateClimate(
        int worldWidth,
        int worldHeight,
        int[,] elevations)
    {
        var temperature = new float[worldWidth, worldHeight];
        var humidity = new float[worldWidth, worldHeight];
        var biomes = new Biome[worldWidth, worldHeight];
        var temperatureAmplitude = new float[worldWidth, worldHeight];

        for (var y = 0; y < worldHeight; y++)
        {
            // Latitude factor: 0 at equator (middle), 1 at poles
            var latitudeFactor = Math.Abs(y - worldHeight / 2.0f) / (worldHeight / 2.0f);

            for (var x = 0; x < worldWidth; x++)
            {
                var elevation = elevations[x, y];
                var isWater = elevation <= 3;

                // Temperature: base -50 to 30, decreases with latitude and elevation
                var baseTemp = 30.0f - 80.0f * latitudeFactor;
                var elevationTempModifier = -0.5f * elevation; // colder at higher elevation
                temperature[x, y] = baseTemp + elevationTempModifier;

                // Humidity: higher near water, lower at high elevation
                var distanceToWater = CalculateDistanceToWater(x, y, worldWidth, worldHeight, elevations);
                var humidityBase = isWater ? 1.0f : Math.Max(0.1f, 1.0f - distanceToWater * 0.1f);
                var elevationHumidityModifier = -0.05f * elevation;
                humidity[x, y] = Math.Clamp(humidityBase + elevationHumidityModifier, 0.0f, 1.0f);

                // Seasonal amplitude: larger at higher latitudes
                temperatureAmplitude[x, y] = 10.0f + 20.0f * latitudeFactor;

                // Determine biome
                biomes[x, y] = DetermineBiome(temperature[x, y], humidity[x, y], elevation, isWater);
            }
        }

        return (temperature, humidity, biomes, temperatureAmplitude);
    }

    private static int CalculateDistanceToWater(int x, int y, int worldWidth, int worldHeight, int[,] elevations)
    {
        // Simple flood fill or BFS to find distance to nearest water
        // For simplicity, use a small radius check
        var minDist = int.MaxValue;
        for (var dy = -5; dy <= 5; dy++)
        {
            for (var dx = -5; dx <= 5; dx++)
            {
                var nx = x + dx;
                var ny = y + dy;
                if (nx < 0 || nx >= worldWidth || ny < 0 || ny >= worldHeight) continue;
                if (elevations[nx, ny] <= 3)
                {
                    var dist = Math.Abs(dx) + Math.Abs(dy); // Manhattan distance
                    minDist = Math.Min(minDist, dist);
                }
            }
        }
        return minDist == int.MaxValue ? 10 : minDist; // default if no water nearby
    }

    private static Biome DetermineBiome(float temp, float humidity, int elevation, bool isWater)
    {
        if (isWater)
            return Biome.Ocean;

        if (elevation >= 8 && temp < -10)
            return Biome.IceCap;

        if (temp < -10)
            return Biome.Tundra;

        if (temp < 5)
        {
            if (humidity > 0.5f)
                return Biome.Taiga;
            else
                return Biome.Tundra;
        }

        if (temp < 15)
        {
            if (humidity > 0.6f)
                return Biome.TemperateForest;
            else
                return Biome.Grassland;
        }

        if (temp < 25)
        {
            if (humidity > 0.7f)
                return Biome.TropicalForest;
            else if (humidity > 0.4f)
                return Biome.Savanna;
            else
                return Biome.Desert;
        }

        // Hot climates
        if (humidity > 0.5f)
            return Biome.TropicalForest;
        else
            return Biome.Desert;
    }
}