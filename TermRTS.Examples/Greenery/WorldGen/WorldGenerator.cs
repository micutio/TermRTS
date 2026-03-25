using System.Buffers;
using System.Numerics;
using SimplexNoise;

namespace TermRTS.Examples.Greenery.WorldGen;

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

public class WorldGenerationResult(
    byte[,] elevation,
    SurfaceFeature[,] surface,
    float[,] temperature,
    float[,] humidity,
    Biome[,] biomes,
    float[,] temperatureAmplitude)
{
    public byte[,] Elevation { get; } = elevation;
    public SurfaceFeature[,] Surface { get; } = surface;
    public float[,] Temperature { get; } = temperature;
    public float[,] Humidity { get; } = humidity;
    public Biome[,] Biomes { get; } = biomes;
    public float[,] TemperatureAmplitude { get; } = temperatureAmplitude;
}

public sealed class WorldBuffer<T> : IDisposable
{
    private readonly T[] _rawArray;
    public Memory<T> Memory { get; }

    public WorldBuffer(int size)
    {
        // Rent from the global shared pool
        _rawArray = ArrayPool<T>.Shared.Rent(size);

        // Wrap ONLY the size we need into Memory
        Memory = _rawArray.AsMemory(0, size);
    }

    public void Dispose()
    {
        // Return to the pool so it can be reused
        ArrayPool<T>.Shared.Return(_rawArray);
    }
}

public class CylinderWorld : IWorldGen
{
    #region Constants

    // Constants for elevation thresholds
    private const int LandElevationThreshold = 4;
    private const int SeaLevelElevation = 3;
    private const int HighMountainThreshold = 7;
    private const int SnowThreshold = 8;
    private const int GlacierThreshold = 9;

    // Constants for base elevations
    private const float ContinentalBaseElevation = 5.0f;
    private const float OceanicBaseElevation = -5.0f;

    // Constants for coastal slopes
    private const float MaxCoastalSlope = 9.0f;

    // Constants for volcanic features
    private const float VolcanicResistance = 0.1f;
    private const float HotspotMinStrength = 0.3f;
    private const float LavaHotspotThreshold = 0.7f;
    private const float CraterElevationThreshold = 6;
    private const float CinderElevationThreshold = 5;
    private const float CalderaElevationThreshold = 7;
    private const float ShieldVolcanoThreshold = 5;

    // Constants for erosion
    private const int SimpleErosionIterations = 5;
    private const float SimpleTalusAngle = 0.5f;
    private const float SimpleErosionRate = 0.1f;

    // Constants for climate
    private const float BaseTempMax = 30.0f;
    private const float BaseTempMin = -50.0f;
    private const float ElevationTempModifier = -0.5f;
    private const float ElevationHumidityModifier = -0.05f;
    private const float MinHumidity = 0.1f;
    private const float BaseTemperatureAmplitude = 10.0f;
    private const float LatitudeAmplitudeModifier = 20.0f;

    // Constants for river carving
    private const int RiverCarveMinElevation = 3;

    // Constants for biome determination
    private const float IceCapTempThreshold = -10.0f;
    private const float IceCapElevationThreshold = 8;
    private const float TundraTempThreshold = -10.0f;
    private const float TaigaTempThreshold = 5.0f;
    private const float TemperateTempThreshold = 15.0f;
    private const float TropicalTempThreshold = 25.0f;
    private const float HighHumidityThreshold = 0.7f;
    private const float MediumHumidityThreshold = 0.6f;
    private const float LowHumidityThreshold = 0.5f;
    private const float VeryLowHumidityThreshold = 0.4f;

    #endregion

    #region Fields

    private readonly Random _rng;
    private readonly int _seed;
    private readonly int _worldWidth;
    private readonly int _worldHeight;
    private readonly int _plateCount;

    // TODO: Distinguish between voronoi cells and tectonic plates
    private WorldBuffer<byte> elevation;
    private WorldBuffer<int> landwaterMap;
    private WorldBuffer<(int, int)> voronoiCells;
    private WorldBuffer<bool> voronoiCellTypes;
    private WorldBuffer<int> voronoiCellIndex;
    private WorldBuffer<bool> plateTypes;
    private WorldBuffer<Vector2> plateMotions;
    private WorldBuffer<float> temperature;
    private WorldBuffer<float> temperatureAmplitude;
    private WorldBuffer<float> humidity;

    #endregion

    #region Properties

    public int VoronoiCellCount { get; set; }

    public float LandRatio { get; set; } = 0.3f;

    // River tuning parameters (adjust at runtime)
    // Lower thresholds create more rivers; higher thresholds make rivers rarer.
    public float RiverFormationThreshold { get; set; } =
        0.01f; // normalized flow-level for river initiation

    public float RiverCarveScale { get; set; } = 3.0f; // scaling factor for river depth from flow
    public float RiverMaxCarveDepth { get; set; } = 2.0f; // maximum depth a river can carve

    // Rainfall tuning parameters (used in river generation)
    public int RainfallWaterDistanceRadius { get; set; } =
        2; // search radius to nearest water for rainfall boost

    public float RainfallWaterDistancePenalty { get; set; } = 0.2f; // weight for distance penalty
    public float RainfallMinValue { get; set; } = 0.1f; // minimum rainfall on land

    public float RainfallElevationDecay { get; set; } =
        0.1f; // how quickly rainfall falls with elevation

    public float RainfallMinModifier { get; set; } = 0.2f; // minimum modifier due to elevation

    // Island chain (hotspot) tuning parameters
    public int MinIslandChains { get; set; } = 3; // minimum number of island chains
    public int MaxIslandChains { get; set; } = 7; // maximum number of island chains
    public int MinChainLength { get; set; } = 3; // minimum hotspots per chain
    public int MaxChainLength { get; set; } = 8; // maximum hotspots per chain
    public int ChainSpacing { get; set; } = 25; // spacing between hotspots in a chain

    public int MinLandDistance { get; set; } =
        8; // minimum distance from land for hotspot placement

    public int MinHotspotRadius { get; set; } = 3; // minimum radius of volcanic cones
    public int MaxHotspotRadius { get; set; } = 8; // maximum radius of volcanic cones
    public float MinHotspotStrength { get; set; } = 0.4f; // minimum elevation strength of hotspots
    public float MaxHotspotStrength { get; set; } = 1.1f; // maximum elevation strength of hotspots

    // Advanced erosion tuning parameters
    public bool UseAdvancedErosion { get; set; } = true; // enable advanced erosion system

    public int ErosionIterations { get; set; } =
        10; // number of erosion iterations (further reduced)

    public float HydraulicErosionRate { get; set; } =
        0.001f; // base hydraulic erosion rate (much more gentle)

    public float SedimentCapacity { get; set; } =
        0.01f; // maximum sediment a water cell can carry (much lower)

    public float DepositionRate { get; set; } =
        0.001f; // rate at which sediment is deposited (much slower)

    public float EvaporationRate { get; set; } = 0.001f; // water evaporation rate
    public float RainRate { get; set; } = 0.00005f; // rainfall rate (much less water)
    public float ThermalErosionRate { get; set; } = 0.05f; // thermal erosion rate (very gentle)
    public float TalusAngle { get; set; } = 0.5f; // minimum slope for material to slide
    public float MinSlope { get; set; } = 0.01f; // minimum slope for water flow
    public float Gravity { get; set; } = 9.81f; // gravity constant for water flow
    public float WaterViscosity { get; set; } = 0.001f; // water viscosity for flow calculations

    #endregion

    #region Constructor

    public CylinderWorld(
        int worldWidth,
        int worldHeight,
        int seed,
        int voronoiCellCount,
        int plateCount)
    {
        // init readonly fields
        _rng = new Random(seed);
        _seed = seed;
        _worldWidth = worldWidth;
        _worldHeight = worldHeight;
        _plateCount = plateCount;

        // init private fields
        elevation = new WorldBuffer<byte>(worldWidth * worldHeight);
        landwaterMap = new WorldBuffer<int>(voronoiCellCount);
        voronoiCells = new WorldBuffer<(int, int)>(voronoiCellCount);
        voronoiCellTypes = new WorldBuffer<bool>(voronoiCellCount);
        voronoiCellIndex = new WorldBuffer<int>(worldWidth * worldHeight);
        plateTypes = new WorldBuffer<bool>(plateCount);
        plateMotions = new WorldBuffer<Vector2>(voronoiCellCount);
        temperature = new WorldBuffer<float>(worldWidth * worldHeight);
        temperatureAmplitude = new WorldBuffer<float>(worldWidth * worldHeight);
        humidity = new WorldBuffer<float>(worldWidth * worldHeight);

        // init properties
        VoronoiCellCount = voronoiCellCount;
    }

    #endregion

    #region IWorldGen Members

    public WorldGenerationResult Generate(int worldWidth, int worldHeight, float landRatio)
    {
        ValidateParameters(worldWidth, worldHeight, landRatio);

        Noise.Seed = _seed;

        InitializePlateTectonics();

        // Associate each grid cell to one of the voronoi cells.
        // TODO: Move this before plate tectonics!
        const int jiggle = 15;
        var (cellElevationsInt, plateIndex) = GenerateLandWaterDistribution(
            worldWidth, worldHeight, jiggle, voronoiCells, landWaterMap);

        // Convert to float for more precise calculations.
        var cellElevations = new float[worldWidth, worldHeight];
        for (var y = 0; y < worldHeight; y++)
        for (var x = 0; x < worldWidth; x++)
            cellElevations[x, y] = cellElevationsInt[x, y];

        // Generate coastal slopes for each voronoi cell.
        var coastalSlopes = GenerateSlopedCoasts(worldWidth, worldHeight, cellElevationsInt);

        // Compute plate tectonics influence (mountains/trenches along plate boundaries).
        var tectonicAdjustment = ComputePlateTectonicHeight(
            worldWidth,
            worldHeight,
            plateIndex,
            voronoiCells,
            plateTypes,
            plateMotions);

        // Generate hotspots (mantle plumes creating volcanic islands/seamounts).
        var hotspotMap = GenerateHotspots(
            worldWidth,
            worldHeight,
            _seed,
            _rng,
            plateMotions,
            voronoiCells,
            plateTypes,
            MinIslandChains,
            MaxIslandChains,
            MinChainLength,
            MaxChainLength,
            ChainSpacing,
            MinHotspotRadius,
            MaxHotspotRadius,
            MinHotspotStrength,
            MaxHotspotStrength);

        // For each voronoi land cell, apply perlin or simplex noise to generate height.
        const float noiseScale = 1.0f;
        const int octaves = 4;
        const float persistence = 0.75f; //?
        const float lacunarity = 1.8f; //?
        var offset = new Vector2(1f, 1f);
        var noiseField = GenerateNoiseMap(
            worldWidth,
            worldHeight,
            _seed,
            noiseScale,
            octaves,
            persistence,
            lacunarity,
            offset);

        ApplyNoiseAndSlopes(
            worldWidth,
            worldHeight,
            cellElevationsInt,
            cellElevations,
            coastalSlopes,
            hotspotMap,
            noiseField,
            tectonicAdjustment);

        // Generate climate (temperature, humidity, biomes, seasonal effects) - moved before erosion
        var (temperature, humidity, biomes, temperatureAmplitude) =
            GenerateClimate(worldWidth, worldHeight, cellElevationsInt);

        // Apply erosion to smooth terrain and create realistic features
        if (UseAdvancedErosion)
            ApplyAdvancedErosion(
                worldWidth,
                worldHeight,
                cellElevations,
                hotspotMap,
                ErosionIterations,
                HydraulicErosionRate,
                SedimentCapacity,
                DepositionRate,
                EvaporationRate,
                RainRate,
                ThermalErosionRate,
                TalusAngle,
                MinSlope,
                Gravity,
                WaterViscosity,
                biomes,
                humidity);
        else
            ApplyErosion(worldWidth, worldHeight, ref cellElevations);

        // Generate rivers based on rainfall and elevation (tunable via public properties)
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

        // Apply mountain details (ridges, snow, glacier, lava)
        var surfaceMap = new SurfaceFeature[worldWidth, worldHeight];
        ApplyMountainDetails(
            worldWidth,
            worldHeight,
            cellElevations,
            surfaceMap,
            plateIndex,
            plateTypes,
            hotspotMap,
            riverMap);

        // Apply coastal features (beach, cliff, fjord)
        ApplyCoastalFeatures(worldWidth, worldHeight, cellElevations, surfaceMap);

        // Convert to final integer elevations, allowing negative values to become 0 (deep trenches)
        var finalElevations = new int[worldWidth, worldHeight];
        for (var y = 0; y < worldHeight; y += 1)
        for (var x = 0; x < worldWidth; x += 1)
            finalElevations[x, y] =
                Math.Clamp((int)Math.Round(cellElevations[x, y]), 0, GlacierThreshold);

        var world = new byte[worldWidth, worldHeight];
        for (var y = 0; y < worldHeight; y += 1)
        for (var x = 0; x < worldWidth; x += 1)
            world[x, y] = Convert.ToByte(finalElevations[x, y]);

        return new WorldGenerationResult(world, surfaceMap, temperature, humidity, biomes,
            temperatureAmplitude);
    }

    #endregion

    #region Validation

    private static void ValidateParameters(int worldWidth, int worldHeight, float landRatio)
    {
        if (worldWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(worldWidth),
                "World width must be greater than 0.");

        if (worldHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(worldHeight),
                "World height must be greater than 0.");

        if (landRatio < 0.0f || landRatio > 1.0f)
            throw new ArgumentOutOfRangeException(nameof(landRatio),
                "Land ratio must be between 0 and 1.");
    }

    #endregion


    #region Tectonics

    /// <summary>
    ///     Initializes plates and tectonic parameters.
    /// </summary>
    /// <param name="worldWidth">Width of the world in grid cells.</param>
    /// <param name="worldHeight">Height of the world in grid cells.</param>
    /// <param name="landRatio">Ratio of land, i.e.: 1 - `landRatio` = ratio of water.</param>
    /// <returns>Voronoi cells and their types, plate motions and land/water distribution.</returns>
    private void InitializePlateTectonics()
    {
        var vCells = voronoiCells.Memory.Span;
        // step 1: randomly sample <cellCount> coordinates of the grid as voronoi cell seeds
        for (var i = 0; i < VoronoiCellCount; i += 1)
            vCells[i] = (_rng.Next(_worldWidth), _rng.Next(_worldHeight));

        // step 2: assign plate types and motions, and infer basic water/land by plate type
        // true = continental, false = oceanic
        GeneratePlateTypes(VoronoiCellCount, LandRatio);
        GeneratePlateMotions(VoronoiCellCount);

        var landWater = landwaterMap.Memory.Span;
        var pTypes = voronoiCellTypes.Memory.Span;
        for (var i = 0; i < VoronoiCellCount; i += 1)
            // Lower oceanic plates to create deeper oceans
            landWater[i] = pTypes[i] ? LandElevationThreshold : SeaLevelElevation;
    }

    /// <summary>
    ///     Generates the type for each plate: true == land, false == water.
    /// </summary>
    /// <param name="plateCount">How many plates we have.</param>
    /// <param name="landRatio">Percentage of land on this map.</param>
    /// <returns>Array of types per plate index.</returns>
    private void GeneratePlateTypes(int plateCount, float landRatio)
    {
        var pTypes = voronoiCellTypes.Memory.Span;
        for (var i = 0; i < plateCount; i += 1)
            pTypes[i] = _rng.NextDouble() < landRatio;
    }

    /// <summary>
    ///     Generates a motion vector for each plate.
    /// </summary>
    /// <param name="plateCount">How many plates we have</param>
    /// <returns>Array of plate motion vector per plate index.</returns>
    private Vector2[] GeneratePlateMotions(int plateCount)
    {
        var motions = new Vector2[plateCount];
        for (var i = 0; i < plateCount; i += 1)
        {
            var angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
            // TODO: Play around with the speed formula.
            var speed = (float)(_rng.NextDouble() * 0.5 + 0.1);
            motions[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;
        }

        return motions;
    }

    /// <summary>
    ///     Assigns an elevation and plate to each single cell on the map.
    /// </summary>
    /// <param name="worldWidth">Width of the world in grid cells.</param>
    /// <param name="worldHeight">Height of the world in grid cells.</param>
    /// <param name="jiggle">Magnitude of the random jiggle.</param>
    /// <param name="voronoiCells">Voronoi cell center locations for this map.</param>
    /// <param name="landWaterMap">Land/water distribution by Voronoi cell index.</param>
    /// <returns></returns>
    private static (int[,] cellElevations, int[,] plateIndex) GenerateLandWaterDistribution(
        int worldWidth,
        int worldHeight,
        int jiggle,
        in IList<(int, int)> voronoiCells,
        in int[] landWaterMap)
    {
        // TODO: Play around with noise scale.
        const float scale = .08f;
        var cellElevations = new int[worldWidth, worldHeight];
        var plateIndex = new int[worldWidth, worldHeight];
        var jiggleNoise = Noise.Calc2D(worldWidth, worldHeight, scale);

        for (var y = 0; y < worldHeight; y += 1)
        for (var x = 0; x < worldWidth; x += 1)
        {
            var jiggledX = x + (125.5 - jiggleNoise[x, y]) / 255.0f * jiggle;
            var jiggledY = y + (125.5 - jiggleNoise[x, y]) / 255.0f * jiggle;

            var minDistSq = double.MaxValue;
            var winnerPlate = 0;
            for (var i = 0; i < voronoiCells.Count; i += 1)
            {
                var vX = voronoiCells[i].Item1;
                var vY = voronoiCells[i].Item2;
                var dx = vX - jiggledX;
                var dy = vY - jiggledY;
                var distSq = GetCylindricalDistanceSq(worldWidth, x, y, dx, dy);

                if (distSq >= minDistSq) continue;
                minDistSq = distSq;
                winnerPlate = i;
            }

            plateIndex[x, y] = winnerPlate;
            cellElevations[x, y] = landWaterMap[winnerPlate];
        }

        return (cellElevations, plateIndex);
    }

    // TODO: Check whether this conflicts with other steps or still necessary.

    /// <summary>
    ///     Generates a gentle fall-off towards costal cells (elevation == 4)
    /// </summary>
    /// <param name="worldWidth"></param>
    /// <param name="worldHeight"></param>
    /// <param name="cellElevations"></param>
    /// <returns>A grid of sloping elevation values for coast lines.</returns>
    private static float[,] GenerateSlopedCoasts(
        int worldWidth,
        int worldHeight,
        in int[,] cellElevations)
    {
        // 1 Initialize all cells to 9; then set boundary (land adjacent to water) to 0 and enqueue.
        var coastalSlopes = new float[worldWidth, worldHeight];
        for (var y = 0; y < worldHeight; y += 1)
        for (var x = 0; x < worldWidth; x += 1)
            coastalSlopes[x, y] = MaxCoastalSlope;

        var q = new Queue<(int, int)>(worldWidth * worldHeight);
        for (var y = 1; y < worldHeight - 1; y += 1)
        for (var x = 1; x < worldWidth - 1; x += 1)
        {
            if (cellElevations[x, y] != LandElevationThreshold) continue;

            // Must have at least one water cell in its neighbourhood.
            if (cellElevations[x, y - 1] != SeaLevelElevation && // north
                cellElevations[x + 1, y] != SeaLevelElevation && // east
                cellElevations[x, y + 1] != SeaLevelElevation && // south
                cellElevations[x - 1, y] != SeaLevelElevation) continue; // west

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
                var neighX = GetWrappedX(worldWidth, x + dirX);
                var neighY = y + dirY;

                if (neighY < 0
                    || neighY >= worldHeight
                    || coastalSlopes[neighX, neighY] <= elevation) continue;

                coastalSlopes[neighX, neighY] = Math.Min(elevation + 1, 9);

                if (elevation < 8)
                    q.Enqueue((neighX, neighY));
            }
        }

        return coastalSlopes;
    }

    /// <summary>
    ///     Generate elevation differences at tectonic plate boundaries,
    ///     e.g.: Mountains and seamounts at plate convergences and
    ///     trenches at plate divergences.
    ///     These changes will only affect world cells located at tectonic plate boundaries.
    /// </summary>
    /// <param name="worldWidth">Width of the world in grid cells.</param>
    /// <param name="worldHeight">Height of the world in grid cells.</param>
    /// <param name="plateIndex">Tectonic plate index for each cell in the world.</param>
    /// <param name="plateCenters">List of center points of tectonic plates.</param>
    /// <param name="plateTypes">Tectonic plate types (true = continent, false = sea).</param>
    /// <param name="plateMotions"></param>
    /// <returns>Elevation changes for grid cells.</returns>
    private static float[,] ComputePlateTectonicHeight(
        int worldWidth,
        int worldHeight,
        in int[,] plateIndex,
        in IList<(int, int)> plateCenters,
        in bool[] plateTypes,
        in Vector2[] plateMotions)
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
                var nx = GetWrappedX(worldWidth, x + dx);
                var ny = y + dy;
                if (ny < 0 || ny >= worldHeight) continue;

                var neighbourPlate = plateIndex[nx, ny];
                if (neighbourPlate == currentPlate) continue;

                var pA = plateCenters[currentPlate];
                var pB = plateCenters[neighbourPlate];
                var direction = new Vector2(pB.Item1 - pA.Item1, pB.Item2 - pA.Item2);
                if (direction.LengthSquared() < 0.0001f) continue;

                var normal = Vector2.Normalize(direction);
                var relativeMotion = plateMotions[neighbourPlate] - plateMotions[currentPlate];
                var stress = Vector2.Dot(relativeMotion, normal);
                var convergence = MathF.Max(0f, stress);
                var divergence = MathF.Max(0f, -stress);
                // Only true if both are continents.
                var continentalInteraction = plateTypes[currentPlate] && plateTypes[neighbourPlate];
                // Only true if one plate is continental and the other is oceanic.
                var mixedInteraction = plateTypes[currentPlate] ^ plateTypes[neighbourPlate];

                if (convergence > 0)
                {
                    if (continentalInteraction)
                        // Higher mountains for continental convergence.
                        // TODO: turn into adjustable parameter
                        totalDelta += convergence * 3.2f;
                    else if (mixedInteraction)
                        // Lesser mountains for mixed convergence.
                        // TODO: turn into adjustable parameter
                        totalDelta += convergence * 4.5f;
                    else
                        // Small bumps for oceanic convergence.
                        // TODO: Does this influence hotspots and hotspot island chains?
                        // TODO: turn into adjustable parameter
                        totalDelta += convergence * 1.7f;
                }
                else if (divergence > 0)
                {
                    if (!plateTypes[currentPlate] && !plateTypes[neighbourPlate])
                        // Very deep trenches for oceanic-oceanic divergence.
                        // TODO: turn into adjustable parameter
                        totalDelta -= divergence * 5.0f;
                    else
                        // Shallower trenches for all other divergences
                        // TODO: turn into adjustable parameter
                        totalDelta -= divergence * 2.4f;
                }
            }

            tectonicDelta[x, y] = totalDelta;
        }

        return tectonicDelta;
    }

    // TODO: Turn numeric parameters in to constants and later tweakable Properties
    /// <summary>
    /// </summary>
    /// <param name="worldWidth"></param>
    /// <param name="worldHeight"></param>
    /// <param name="seed"></param>
    /// <param name="rng"></param>
    /// <param name="plateMotions"></param>
    /// <param name="plateCenters"></param>
    /// <param name="plateTypes"></param>
    /// <param name="minIslandChains"></param>
    /// <param name="maxIslandChains"></param>
    /// <param name="minChainLength"></param>
    /// <param name="maxChainLength"></param>
    /// <param name="chainSpacing"></param>
    /// <param name="minHotspotRadius"></param>
    /// <param name="maxHotspotRadius"></param>
    /// <param name="minHotspotStrength"></param>
    /// <param name="maxHotspotStrength"></param>
    /// <returns></returns>
    private static float[,] GenerateHotspots(
        int worldWidth,
        int worldHeight,
        int seed,
        Random rng,
        in Vector2[] plateMotions,
        in IList<(int, int)> plateCenters,
        in bool[] plateTypes,
        int minIslandChains,
        int maxIslandChains,
        int minChainLength,
        int maxChainLength,
        int chainSpacing,
        int minHotspotRadius,
        int maxHotspotRadius,
        float minHotspotStrength,
        float maxHotspotStrength)
    {
        var hotspotMap = new float[worldWidth, worldHeight];
        var chainCount =
            rng.Next(minIslandChains,
                maxIslandChains + 1); // +1 because Next upper bound is exclusive

        // TODO: Why the need for a different rng?
        var prng = new Random(seed + 12345); // Different seed for hotspots
        var oceanPlateIds = new List<int>();
        for (var i = 0; i < plateTypes.Length; i++)
            if (!plateTypes[i])
                oceanPlateIds.Add(i);

        for (var chain = 0; chain < chainCount; chain++)
        {
            if (oceanPlateIds.Count == 0) continue;

            var oceanPlateId = oceanPlateIds[prng.Next(oceanPlateIds.Count)];
            oceanPlateIds.Remove(oceanPlateId);
            var (startX, startY) = plateCenters[oceanPlateId];

            // Create a chain of hotspots along a plate motion direction.
            var chainLength = prng.Next(minChainLength, maxChainLength + 1);

            // Use a random plate motion direction for the chain, or create a random direction
            var chainDirection = plateMotions.Length > 0
                ? plateMotions[prng.Next(plateMotions.Length)]
                : new Vector2(
                    (float)(prng.NextDouble() - 0.5) * 2,
                    (float)(prng.NextDouble() - 0.5) * 2);

            // Normalize and scale the direction
            var length = MathF.Sqrt(
                chainDirection.X * chainDirection.X +
                chainDirection.Y * chainDirection.Y);
            if (length > 0)
                chainDirection = chainDirection / length * (float)(prng.NextDouble() * 2 + 1);

            for (var i = 0; i < chainLength; i++)
            {
                // Calculate position along the chain with configurable spacing
                var offsetX = (int)(chainDirection.X * i * chainSpacing);
                var offsetY = (int)(chainDirection.Y * i * chainSpacing);
                var centerX = GetWrappedX(worldWidth, startX + offsetX);
                var centerY = startY + offsetY;

                // Keep within bounds with some wrapping
                // centerX = (centerX % worldWidth + worldWidth) % worldWidth;
                // centerY = (centerY % worldHeight + worldHeight) % worldHeight;
                if (offsetY < 0 || offsetY >= worldHeight) continue;

                var radius = prng.Next(minHotspotRadius, maxHotspotRadius + 1);
                var strength =
                    (float)(prng.NextDouble() * (maxHotspotStrength - minHotspotStrength) +
                            minHotspotStrength);

                // Create a volcanic cone shape with exponential falloff (more realistic)
                // TODO: This looks very inefficient.
                var minY = Math.Max(0, centerY - radius);
                var maxY = Math.Min(worldHeight, centerY + radius);
                var minX = Math.Max(0, centerX - radius);
                var maxX = Math.Min(worldWidth, centerX + radius);
                for (var y = minY; y < maxY; y++)
                for (var x = minX; x < maxX; x++)
                {
                    var dx = x - centerX;
                    var dy = y - centerY;
                    var distance = MathF.Sqrt(dx * dx + dy * dy);

                    if (distance > radius) continue;

                    // Exponential falloff for more realistic volcanic shape
                    // TODO: make falloff a tweakable parameter
                    var normalizedDist = distance / radius;
                    var coneHeight = MathF.Exp(-normalizedDist * 3.0f) * strength;

                    // Add some noise to make it look more volcanic
                    // TODO: make noise a tweakable parameter
                    var noise = Noise.CalcPixel2D(x * 20, y * 20, 0.05f) * 0.5f + 0.5f;
                    coneHeight *= 0.7f + noise * 0.6f;

                    hotspotMap[x, y] += coneHeight;
                }
            }
        }

        return hotspotMap;
    }

    #endregion

    #region Noise Map

    /// <summary>
    ///     Generates a map of pseudo-random Perlin noise.
    /// </summary>
    /// <param name="mapWidth">Map width in grid cells</param>
    /// <param name="mapHeight">Map height in grid cells</param>
    /// <param name="seed">Seed of the random number generator</param>
    /// <param name="scale">Scale of the noise</param>
    /// <param name="octaves">How many layers of noise to generate</param>
    /// <param name="persistence">Less -> smoother, more -> rougher?</param>
    /// <param name="lacunarity">Gappiness, how many and/or large holes</param>
    /// <param name="offset">Offset between layers?</param>
    /// <returns></returns>
    private static float[,] GenerateNoiseMap(
        int mapWidth,
        int mapHeight,
        int seed,
        float scale,
        int octaves,
        float persistence,
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

                // var perlinValue = Noise.CalcPixel2D(sampleX, sampleY, noiseScale) * 2 - 1;
                var perlinValue =
                    GetCylindricalNoise(sampleX, sampleY, mapWidth, noiseScale, Noise.CalcPixel3D);

                noiseHeight += perlinValue * amplitude;
                amplitude *= persistence;
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

    private static void ApplyNoiseAndSlopes(
        int worldWidth,
        int worldHeight,
        in int[,] cellElevationsInt,
        in float[,] cellElevations,
        in float[,] coastalSlopes,
        in float[,] hotspotMap,
        in float[,] noiseField,
        in float[,] tectonicAdjustment)
    {
        // Apply noise and slopes to elevation map.
        for (var y = 0; y < worldHeight; y += 1)
        for (var x = 0; x < worldWidth; x += 1)
        {
            var baseElevation = cellElevationsInt[x, y] >= LandElevationThreshold
                ? ContinentalBaseElevation
                : OceanicBaseElevation; // Continental plates (>=4) get higher base, oceanic (<4) get much lower base for deep trenches
            var slopeFactor = coastalSlopes[x, y] / MaxCoastalSlope;
            var normalizedNoise = noiseField[x, y] / 255.0;
            var tectonic = tectonicAdjustment[x, y];
            var hotspot = hotspotMap[x, y];

            // For oceanic plates, reduce noise impact to allow deeper trenches
            var cellElevationContribution = cellElevationsInt[x, y] >= LandElevationThreshold
                ? cellElevationsInt[x, y]
                : 0;
            var noiseMultiplier =
                cellElevationsInt[x, y] >= LandElevationThreshold
                    ? 1.0f
                    : 0.3f; // Less noise in oceans
            var elevation = cellElevationContribution +
                            baseElevation * slopeFactor * normalizedNoise * noiseMultiplier +
                            tectonic + hotspot;

            // Store as float, don't clamp yet
            cellElevations[x, y] = (float)elevation;
        }
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + t * (b - a);
    }

    private static float InverseLerp(float a, float b, float t)
    {
        return (t - a) / (b - a);
    }

    // Delegate for your specific 3D noise function.
    // It should take X, Y, Z coordinates and return a noise value.
    private delegate float Noise3DFunc(int x, int y, int z, float scale);

    /// <summary>
    /// Gets a seamless cylindrical noise value for a 2D grid coordinate.
    /// </summary>
    /// <param name="x">The X coordinate on your 2D grid.</param>
    /// <param name="y">The Y coordinate on your 2D grid.</param>
    /// <param name="mapWidth">The total width of the map (used for wrapping).</param>
    /// <param name="noiseScale">The frequency/zoom level of the noise.</param>
    /// <param name="noiseFunction">Your 3D noise generator method.</param>
    /// <returns>The sampled noise value.</returns>
    private static float GetCylindricalNoise(
        int x,
        int y,
        int mapWidth,
        float noiseScale,
        Noise3DFunc noiseFunction)
    {
        // 1. Convert the linear X coordinate to an angle around the cylinder (0 to 2*PI)
        var angle = (double)x / mapWidth * (2.0 * Math.PI);

        // 2. Calculate the cylinder's radius.
        // By setting the circumference equal to the map width (C = 2 * PI * R),
        // we ensure that moving 1 unit in X travels exactly 1 unit along the 3D cylinder's edge.
        // This guarantees the noise scale on the X axis perfectly matches the Y axis.
        var radius = mapWidth / (2.0 * Math.PI);

        // 3. Convert polar coordinates to 3D Cartesian coordinates
        // We multiply by noiseScale here just like you would with standard 2D noise: noise(x * scale, y * scale)
        var nx = radius * Math.Cos(angle) * noiseScale;
        var nz = radius * Math.Sin(angle) * noiseScale;
        var ny = y * noiseScale;

        // 4. Sample and return the 3D noise
        return noiseFunction(Convert.ToInt32(nx), Convert.ToInt32(ny), Convert.ToInt32(nz),
            noiseScale);
    }

    #endregion

    #region Climate and Biomes

    /// <summary>
    ///     Generates world maps for various climate features.
    /// </summary>
    /// <param name="worldWidth">World width in cells.</param>
    /// <param name="worldHeight">World height in cells.</param>
    /// <param name="elevations">Elevation map of the world.</param>
    /// <returns>
    ///     World maps for:
    ///     - temperature
    ///     - humidity
    ///     - biomes
    ///     - temperature amplitude
    /// </returns>
    private static (float[,], float[,], Biome[,], float[,]) GenerateClimate(
        int worldWidth,
        int worldHeight,
        in int[,] elevations)
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
                var baseTemp = BaseTempMax - (BaseTempMax - BaseTempMin) * latitudeFactor;
                var elevationTempModifier =
                    ElevationTempModifier * elevation; // colder at higher elevation
                temperature[x, y] = baseTemp + elevationTempModifier;

                // Humidity: higher near water, lower at high elevation
                var distanceToWater =
                    CalculateDistanceToWater(x, y, worldWidth, worldHeight, elevations);
                var humidityBase =
                    isWater ? 1.0f : Math.Max(MinHumidity, 1.0f - distanceToWater * 0.1f);
                var elevationHumidityModifier = ElevationHumidityModifier * elevation;
                humidity[x, y] = Math.Clamp(humidityBase + elevationHumidityModifier, 0.0f, 1.0f);

                // Seasonal amplitude: larger at higher latitudes
                temperatureAmplitude[x, y] = BaseTemperatureAmplitude +
                                             LatitudeAmplitudeModifier * latitudeFactor;


                // Determine biome
                biomes[x, y] =
                    DetermineBiome(temperature[x, y], humidity[x, y], elevation, isWater);
            }
        }

        return (temperature, humidity, biomes, temperatureAmplitude);
    }

    // TODO: Refine dist to water, make dist adjustable maybe?

    /// <summary>
    ///     Calculate the distance of a cell to water, if water is within 5 cells..
    /// </summary>
    /// <param name="x">X-coordinate of the cell.</param>
    /// <param name="y">Y-coordinate of the cell.</param>
    /// <param name="worldWidth">Width of the world in cells.</param>
    /// <param name="worldHeight">Height of the world in cells.</param>
    /// <param name="elevations">Elevation</param>
    /// <returns>Distance of the cell at the given coordinate from the nearest water cell.</returns>
    private static int CalculateDistanceToWater(int x, int y, int worldWidth, int worldHeight,
        int[,] elevations)
    {
        // Simple flood fill or BFS to find distance to nearest water
        // For simplicity, use a small radius check
        var minDist = int.MaxValue;
        for (var dy = -5; dy <= 5; dy++)
        for (var dx = -5; dx <= 5; dx++)
        {
            var nx = GetWrappedX(worldWidth, x + dx);
            var ny = y + dy;
            if (ny < 0 || ny >= worldHeight) continue;
            if (elevations[nx, ny] > 3) continue;

            var dist = Math.Abs(dx) + Math.Abs(dy); // Manhattan distance
            minDist = Math.Min(minDist, dist);
        }

        return minDist == int.MaxValue ? 10 : minDist; // default if no water nearby
    }

    /// <summary>
    ///     Determine the biome of a cell by its properties.
    /// </summary>
    /// <param name="temp">Temperature of the cell.</param>
    /// <param name="humidity">Humidity of the cell.</param>
    /// <param name="elevation">Elevation of the cell.</param>
    /// <param name="isWater">True if the cell is sea, false otherwise.</param>
    /// <returns>Biome of the cell.</returns>
    private static Biome DetermineBiome(float temp, float humidity, int elevation, bool isWater)
    {
        if (isWater)
            return Biome.Ocean;

        if (elevation >= IceCapElevationThreshold && temp < IceCapTempThreshold)
            return Biome.IceCap;

        return temp switch
        {
            < TundraTempThreshold => Biome.Tundra,
            < TaigaTempThreshold when humidity > LowHumidityThreshold => Biome.Taiga,
            < TaigaTempThreshold => Biome.Tundra,
            < TemperateTempThreshold when humidity > MediumHumidityThreshold => Biome
                .TemperateForest,
            < TemperateTempThreshold => Biome.Grassland,
            < TropicalTempThreshold when humidity > HighHumidityThreshold => Biome.TropicalForest,
            < TropicalTempThreshold when humidity > VeryLowHumidityThreshold => Biome.Savanna,
            < TropicalTempThreshold => Biome.Desert,
            // Hot climates
            _ => humidity > LowHumidityThreshold ? Biome.TropicalForest : Biome.Desert
        };
    }

    #endregion

    #region Erosion

    // TODO: Clarify that this meets the definition of thermal erosion.
    // TODO: Possibly delete in favour of advanced erosion!
    /// <summary>
    ///     Apply thermal and hydraulic erosion to
    /// </summary>
    /// <param name="worldWidth">World width in grid cells</param>
    /// <param name="worldHeight">World height in grid cells</param>
    /// <param name="elevations">Grid of world elevation values</param>
    private static void ApplyErosion(int worldWidth, int worldHeight, ref float[,] elevations)
    {
        // Simple thermal erosion + hydraulic erosion simulation

        for (var iter = 0; iter < SimpleErosionIterations; iter++)
        {
            // TODO: replace shallow copy with deep copy
            var newElevations = (float[,])elevations.Clone();

            for (var y = 1; y < worldHeight - 1; y++)
            for (var x = 0; x < worldWidth; x++)
            {
                var currentHeight = elevations[x, y];
                var lowestNeighbor = currentHeight;
                var lowestX = x;
                var lowestY = y;

                // Find lowest neighbor
                (int, int)[] directions =
                    [(0, -1), (1, 0), (0, 1), (-1, 0), (1, -1), (1, 1), (-1, 1), (-1, -1)];
                foreach (var (dx, dy) in directions)
                {
                    var nx = GetWrappedX(worldWidth, x + dx);
                    var ny = y + dy;
                    var neighborHeight = elevations[nx, ny];

                    if (neighborHeight >= lowestNeighbor) continue;
                    lowestNeighbor = neighborHeight;
                    lowestX = nx;
                    lowestY = ny;
                }

                var slope = currentHeight - lowestNeighbor;

                // Thermal erosion: material slides if slope is too steep, even underwater.
                if (slope > SimpleTalusAngle)
                {
                    var slideAmount = Math.Min(SimpleErosionRate * slope, slope * 0.5f);
                    newElevations[x, y] -= slideAmount;
                    newElevations[lowestX, lowestY] += slideAmount;
                }

                // Hydraulic erosion: water flow simulation (simplified)
                // TODO: Possibly parameterize water surface level (=3)
                var waterFlow = Math.Max(0, currentHeight - 3); // Water surface at 3
                if (waterFlow <= 0) continue;

                var erosionAmount = Math.Min(SimpleErosionRate * waterFlow * 0.1f, 0.5f);
                newElevations[x, y] -= erosionAmount;
            }

            elevations = newElevations;
        }
    }

    /// <summary>
    ///     Apply erosion with hydraulic simulation, sediment transport and thermal erosion.
    /// </summary>
    /// <param name="worldWidth">World width in grid cells.</param>
    /// <param name="worldHeight">World Height in grid cells.</param>
    /// <param name="elevations">Grid of elevation values.</param>
    /// <param name="hotspotMap">Map of hotspots in the world.</param>
    /// <param name="iterations">Count of simulation iterations to perform.</param>
    /// <param name="hydraulicErosionRate">Rate of hydraulic erosion per step.</param>
    /// <param name="sedimentCapacity">Capacity of sediment one cell can have.</param>
    /// <param name="depositionRate">How much sediment is deposited by hydraulic erosion.</param>
    /// <param name="evaporationRate">How much water evaporates per step.</param>
    /// <param name="rainRate">How much it rains per step.</param>
    /// <param name="thermalErosionRate">Rate of erosion of soil down slope.</param>
    /// <param name="talusAngle">Slope angle after which erosion occurs.</param>
    /// <param name="minSlope">Minimum slope for water to flow.</param>
    /// <param name="gravity">Gravitation coefficient.</param>
    /// <param name="waterViscosity">How much water flows.</param>
    /// <param name="biomes">Map of biomes on the map.</param>
    /// <param name="humidity">Map of humidity across the map.</param>
    private static void ApplyAdvancedErosion(
        int worldWidth,
        int worldHeight,
        in float[,] elevations,
        in float[,] hotspotMap,
        int iterations,
        float hydraulicErosionRate,
        float sedimentCapacity,
        float depositionRate,
        float evaporationRate,
        float rainRate,
        float thermalErosionRate,
        float talusAngle,
        float minSlope,
        float gravity,
        float waterViscosity,
        in Biome[,] biomes,
        in float[,] humidity)
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
                // TODO: Possibly turn localRainRate coefficients into constants or tweakable properties
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
            // TODO: Make this work with wrapped X-coordinates.
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
            // TODO: replace shallow copy with deep copy
            var newTerrain = (float[,])terrain.Clone();
            var newWater = (float[,])water.Clone();
            var newSediment = (float[,])sediment.Clone();

            for (var y = 1; y < worldHeight - 1; y++)
            for (var x = 1; x < worldWidth - 1; x++)
            {
                var currentWater = water[x, y];
                if (currentWater <= 0) continue;

                // Calculate sediment capacity based on water velocity
                var velocity = MathF.Sqrt(velocityX[x, y] * velocityX[x, y] +
                                          velocityY[x, y] * velocityY[x, y]);
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
                        // 90% less erosion in volcanic areas
                        volcanicResistance = VolcanicResistance;

                    erodeAmount *= volcanicResistance;
                    // Don't erode too much. // TODO: Do we really need this guard?
                    erodeAmount = Math.Min(erodeAmount, terrain[x, y] * 0.1f);
                    newTerrain[x, y] -= erodeAmount;
                    newSediment[x, y] += erodeAmount;
                }

                // Water flow to neighboring cells
                var totalOutflow = 0f;
                (int, int)[] directions = [(0, -1), (1, 0), (0, 1), (-1, 0)];

                foreach (var (dx, dy) in directions)
                {
                    var nx = GetWrappedX(worldWidth, x + dx);
                    var ny = y + dy;

                    if (ny < 0 || ny >= worldHeight) continue;

                    var neighborSlope = terrain[x, y] - terrain[nx, ny];
                    if (neighborSlope <= 0) continue;

                    var outflow = currentWater * neighborSlope * hydraulicErosionRate;
                    newWater[nx, ny] += outflow * 0.25f; // Distribute to neighbors
                    newSediment[nx, ny] += currentSediment * outflow /
                        Math.Max(currentWater, waterViscosity) * 0.25f;
                    totalOutflow += outflow;
                }

                newWater[x, y] -= totalOutflow;
                newSediment[x, y] -=
                    currentSediment * totalOutflow / Math.Max(currentWater, 0.001f);
            }

            // Step 4: Thermal erosion (material sliding)
            // TODO: Fix reading and writing form/to newTerrain in the same loop!
            // TODO: Make this work with wrapped x-coordinate.
            for (var y = 1; y < worldHeight - 1; y++)
            for (var x = 1; x < worldWidth - 1; x++)
            {
                var currentHeight = newTerrain[x, y];
                var lowestNeighbor = currentHeight;
                var lowestX = x;
                var lowestY = y;

                // Find lowest neighbor
                (int, int)[] directions =
                    [(0, -1), (1, 0), (0, 1), (-1, 0), (1, -1), (1, 1), (-1, 1), (-1, -1)];
                foreach (var (dx, dy) in directions)
                {
                    var nx = x + dx;
                    var ny = y + dy;
                    var neighborHeight = newTerrain[nx, ny];

                    if (neighborHeight >= lowestNeighbor) continue;
                    lowestNeighbor = neighborHeight;
                    lowestX = nx;
                    lowestY = ny;
                }

                var slope = currentHeight - lowestNeighbor;

                // Material slides if slope is too steep
                if (slope <= talusAngle) continue;
                var slideAmount = Math.Min(thermalErosionRate * slope, slope * 0.5f);
                newTerrain[x, y] -= slideAmount;
                newTerrain[lowestX, lowestY] += slideAmount;
            }

            // Step 5: Evaporation (climate-aware)
            for (var y = 0; y < worldHeight; y++)
            for (var x = 0; x < worldWidth; x++)
            {
                // Higher evaporation in dry/hot climates
                var climateModifier = 1.0f;
                var biome = biomes[x, y];
                var humidityValue = humidity[x, y];

                if (biome is Biome.Desert or Biome.Tundra)
                    climateModifier = 2.0f; // 2x evaporation in dry areas
                else if (humidityValue < 0.3f)
                    climateModifier = 1.5f; // 1.5x evaporation in low humidity areas

                var adjustedEvaporation = evaporationRate * climateModifier;
                newWater[x, y] = Math.Max(0, newWater[x, y] - adjustedEvaporation);
            }

            // Update arrays
            // TODO: Double buffer to swap between arrays.
            terrain = newTerrain;
            water = newWater;
            sediment = newSediment;
        }

        // Copy back to elevations (keep as float for now)
        for (var y = 0; y < worldHeight; y++)
        for (var x = 0; x < worldWidth; x++)
            elevations[x, y] = terrain[x, y];
    }

    #endregion

    #region Rivers

    // TODO: Check whether to let this be influenced by biome, temperature or humidity as well.

    /// <summary>
    ///     Generates rivers on the map.
    /// </summary>
    /// <param name="worldWidth">Width of the world in grid cells.</param>
    /// <param name="worldHeight">Height of the world in grid cells.</param>
    /// <param name="elevations">Elevation map of the world.</param>
    /// <param name="formationThreshold"></param>
    /// <param name="carveScale"></param>
    /// <param name="maxCarveDepth"></param>
    /// <param name="waterDistanceRadius">Radius for detecting water around a cell.</param>
    /// <param name="distancePenalty">Penalty for rainfall on areas far away from the sea.</param>
    /// <param name="rainfallMin">Minimum amount of rainfall per cell.</param>
    /// <param name="elevationDecay">How much less rain falls per increase in elevation.</param>
    /// <param name="minModifier">Minimum modifier of the elevation penalty.</param>
    /// <returns>Grid of bool values: true if cell belongs to a river, false otherwise.</returns>
    private static bool[,] GenerateRivers(
        int worldWidth,
        int worldHeight,
        in float[,] elevations,
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
        var riverMap = CarveRivers(worldWidth, worldHeight, elevations, flowDirections,
            flowAccumulation, formationThreshold, carveScale, maxCarveDepth);

        // Step 5: Deposit sediment in river valleys (optional)
        DepositSediment(worldWidth, worldHeight, elevations, flowAccumulation);

        return riverMap;
    }

    /// <summary>
    ///     Generate a map of rainfall per cell.
    ///     Rainfall increases with proximity to water and decreases with elevation.
    /// </summary>
    /// <param name="worldWidth">Width of the world in grid cells.</param>
    /// <param name="worldHeight">Height of the world in grid cells.</param>
    /// <param name="elevations">Elevation map of the world.</param>
    /// <param name="waterDistanceRadius">Radius for detecting water around a cell.</param>
    /// <param name="distancePenalty"></param>
    /// <param name="rainfallMin"></param>
    /// <param name="elevationDecay"></param>
    /// <param name="minModifier"></param>
    /// <returns>A grid of rain fall per cell.</returns>
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
            // TODO: Parameterize water surface elevation
            const int waterSurfaceElevation = 3;
            var isWater = elevation <= waterSurfaceElevation; // Water cells get maximum rainfall

            // Distance to nearest water (simplified - just check neighbors)
            var waterDistance = 0f;
            if (!isWater)
            {
                var minDistance = float.MaxValue;
                for (var dy = -waterDistanceRadius; dy <= waterDistanceRadius; dy++)
                for (var dx = -waterDistanceRadius; dx <= waterDistanceRadius; dx++)
                {
                    var nx = GetWrappedX(worldWidth, x + dx);
                    var ny = y + dy;
                    if (ny < 0 || ny >= worldHeight) continue;

                    if (elevations[nx, ny] > waterSurfaceElevation) continue;

                    var distance = MathF.Sqrt(dx * dx + dy * dy);
                    minDistance = MathF.Min(minDistance, distance);
                }

                const float tolerance = 0.1f;
                waterDistance = Math.Abs(minDistance - float.MaxValue) < tolerance
                    ? waterDistanceRadius
                    : minDistance;
            }

            // Rainfall formula: high near water, decreases with elevation and distance
            var baseRainfall = isWater
                ? 1.0f
                : MathF.Max(rainfallMin, 1.0f - waterDistance * distancePenalty);
            var elevationModifier = MathF.Max(minModifier, 1.0f - elevation * elevationDecay);
            rainfall[x, y] = baseRainfall * elevationModifier;
        }

        return rainfall;
    }

    // TODO: See whether we can reuse this flow field for other functions.

    /// <summary>
    ///     Calculates a map of hypothetical water flow directions for each cell.
    /// </summary>
    /// <param name="worldWidth">Width of the world in grid cells.</param>
    /// <param name="worldHeight">Height of the world in grid cells.</param>
    /// <param name="elevations">Elevation map of the world.</param>
    /// <returns>A grid of flow vectors per cell.</returns>
    private static (int, int)[,] CalculateFlowDirections(
        int worldWidth,
        int worldHeight,
        float[,] elevations)
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

                var nx = GetWrappedX(worldWidth, x + dx);
                var ny = y + dy;

                if (ny < 0 || ny >= worldHeight) continue;

                var neighborElevation = elevations[nx, ny];
                var drop = currentElevation - neighborElevation;

                if (drop <= steepestDrop) continue;

                steepestDrop = drop;
                bestDirection = (dx, dy);
            }

            flowDirections[x, y] = bestDirection;
        }

        return flowDirections;
    }

    /// <summary>
    ///     Simulate the flow of water down the elevations and keep track of where it ends up.
    /// </summary>
    /// <param name="worldWidth">Width of the world in grid cells.</param>
    /// <param name="worldHeight">Height of the world in grid cells.</param>
    /// <param name="flowDirections">Direction of flow for each cell.</param>
    /// <param name="rainfall">Mapping of rainfall amount per cell.</param>
    /// <returns>Map of accumulated flow for each cell.</returns>
    private static float[,] AccumulateFlow(
        int worldWidth,
        int worldHeight,
        (int, int)[,] flowDirections,
        float[,] rainfall)
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

                var nx = GetWrappedX(worldWidth, x + dx);
                var ny = y + dy;
                if (ny < 0 || ny >= worldHeight) continue;

                var (fdx, fdy) = flowDirections[nx, ny];
                if (nx + fdx == x && ny + fdy == y) hasIncoming = true;
            }

            if (hasIncoming) continue;
            queue.Enqueue((x, y));
            processed[x, y] = true;
        }

        // Process queue
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            var (dx, dy) = flowDirections[x, y];
            if (dx == 0 && dy == 0) continue;

            var nx = x + dx;
            var ny = y + dy;
            if (nx < 0 || nx >= worldWidth || ny < 0 || ny >= worldHeight) continue;

            flowAccumulation[nx, ny] += flowAccumulation[x, y];

            // Check if all upstream cells are processed
            var allUpstreamProcessed = true;
            for (var dy2 = -1; dy2 <= 1 && allUpstreamProcessed; dy2++)
            for (var dx2 = -1; dx2 <= 1 && allUpstreamProcessed; dx2++)
            {
                if (dx2 == 0 && dy2 == 0) continue;

                var nx2 = GetWrappedX(worldWidth, nx + dx2);
                var ny2 = ny + dy2;
                if (ny2 < 0 || ny2 >= worldHeight) continue;

                var (fdx2, fdy2) = flowDirections[nx2, ny2];
                if (nx2 + fdx2 == nx && ny2 + fdy2 == ny && !processed[nx2, ny2])
                    allUpstreamProcessed = false;
            }

            if (!allUpstreamProcessed || processed[nx, ny]) continue;

            processed[nx, ny] = true;
            queue.Enqueue((nx, ny));
        }

        return flowAccumulation;
    }

    /// <summary>
    ///     Create a boolean river map of the world where true -> belongs to a river, false otherwise.
    /// </summary>
    /// <param name="worldWidth">Width of the world in grid cells.</param>
    /// <param name="worldHeight">Height of the world in grid cells.</param>
    /// <param name="elevations">Elevation map of the world. Is being modified</param>
    /// <param name="flowDirections">Flow direction for each cell.</param>
    /// <param name="flowAccumulation">Accumulated flow for each cell.</param>
    /// <param name="formationThreshold">
    ///     Threshold for how much flow should constitute a river.
    /// </param>
    /// <param name="carveScale"></param>
    /// <param name="maxCarveDepth">Maximum elevation to cut away for the rivers.</param>
    /// <returns></returns>
    private static bool[,] CarveRivers(
        int worldWidth,
        int worldHeight,
        in float[,] elevations,
        (int, int)[,] flowDirections,
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

        var globalThreshold = MathF.Max(0.01f, formationThreshold);

        for (var y = 0; y < worldHeight; y++)
        for (var x = 0; x < worldWidth; x++)
        {
            var normalizedFlow = maxFlow > 0 ? flowAccumulation[x, y] / maxFlow : 0f;
            var currentElevation = elevations[x, y];

            // Only start river source on land
            if (currentElevation < LandElevationThreshold || normalizedFlow <= globalThreshold)
                continue;

            // Trace river path downstream following flow directions, ensuring connectivity
            var cx = x;
            var cy = y;
            var maxSteps = worldWidth + worldHeight;

            while (maxSteps-- > 0)
            {
                if (cy < 0 || cy >= worldHeight)
                    break;

                if (riverMap[cx, cy])
                    break; // Path already included

                riverMap[cx, cy] = true;

                var cellElevation = elevations[cx, cy];
                var depth = MathF.Min(maxCarveDepth,
                    flowAccumulation[cx, cy] / maxFlow * carveScale);
                elevations[cx, cy] = Math.Max(RiverCarveMinElevation, cellElevation - depth);

                if (cellElevation <= SeaLevelElevation)
                    break; // Reached coast

                var (dx, dy) = flowDirections[cx, cy];
                if (dx == 0 && dy == 0)
                    break; // No downhill direction

                var nx = GetWrappedX(worldWidth, cx + dx);
                var ny = cy + dy;

                if (ny < 0 || ny >= worldHeight)
                    break;

                // stop at ocean or already established river cell
                if (elevations[nx, ny] <= SeaLevelElevation || riverMap[nx, ny])
                {
                    riverMap[nx, ny] = elevations[nx, ny] > SeaLevelElevation;
                    break;
                }

                cx = nx;
                cy = ny;
            }
        }

        return riverMap;
    }

    /// <summary>
    ///     Deposits sediments carried along by the rivers.
    /// </summary>
    /// <param name="worldWidth">Width of the world in grid cells.</param>
    /// <param name="worldHeight">Height of the world in grid cells.</param>
    /// <param name="elevations">Elevation map of the world.</param>
    /// <param name="flowAccumulation">Map of flow accumulation per cell.</param>
    private static void DepositSediment(
        int worldWidth,
        int worldHeight,
        in float[,] elevations,
        float[,] flowAccumulation)
    {
        // Simple sediment deposition in low areas
        for (var y = 0; y < worldHeight; y++)
        for (var x = 0; x < worldWidth; x++)
        {
            var currentElevation = elevations[x, y];

            // Deposit sediment in very low areas (potential floodplains)
            if (currentElevation < SeaLevelElevation && flowAccumulation[x, y] > 0)
                // Small sediment deposit
                elevations[x, y] = Math.Min(9, currentElevation + 1);
        }
    }

    #endregion

    #region Mountains

    // TODO: Mountain features could use more work, e.g.: glaciers cna also appear on sea level
    //       in high latitudes.

    /// <summary>
    ///     Applies surface features to mountains, snow, glaciers, rivers and lava.
    /// </summary>
    /// <param name="worldWidth">Width of the world in cells.</param>
    /// <param name="worldHeight">Height of the world in cells.</param>
    /// <param name="elevations">World elevation map.</param>
    /// <param name="surfaceMap">Surface feature map of the world.</param>
    /// <param name="plateIndex">Map of each cell to the plate it belongs to.</param>
    /// <param name="plateTypes">Map of plate indices to land (true) and sea (false). </param>
    /// <param name="hotspotMap">Map of hotspots in the world.</param>
    /// <param name="riverMap">Map of river cells in the world.</param>
    private static void ApplyMountainDetails(
        int worldWidth,
        int worldHeight,
        in float[,] elevations,
        in SurfaceFeature[,] surfaceMap,
        in int[,] plateIndex,
        in bool[] plateTypes,
        in float[,] hotspotMap,
        in bool[,] riverMap)
    {
        for (var y = 0; y < worldHeight; y++)
        for (var x = 0; x < worldWidth; x++)
        {
            surfaceMap[x, y] = SurfaceFeature.None;

            var elevation = elevations[x, y];
            if (elevation >= HighMountainThreshold)
                surfaceMap[x, y] = SurfaceFeature.Mountain;

            if (elevation >= SnowThreshold)
                surfaceMap[x, y] = SurfaceFeature.Snow;

            if (elevation >= GlacierThreshold && !riverMap[x, y])
                surfaceMap[x, y] = SurfaceFeature.Glacier;

            if (riverMap[x, y])
                surfaceMap[x, y] = SurfaceFeature.River;

            if (hotspotMap[x, y] > LavaHotspotThreshold && elevation >= LandElevationThreshold)
                surfaceMap[x, y] = SurfaceFeature.Lava;
        }

        // Add volcanic details around hotspots
        var hotspotCenters = FindHotspotCenters(worldWidth, worldHeight, hotspotMap);
        foreach (var (centerX, centerY, strength) in hotspotCenters)
            AddVolcanicDetails(
                worldWidth,
                worldHeight,
                elevations,
                surfaceMap,
                centerX,
                centerY,
                strength);

        // Plate boundary mountain ridges: continental collisions
        var neighbors = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        for (var y = 0; y < worldHeight; y++)
        for (var x = 0; x < worldWidth; x++)
        {
            var currentPlate = plateIndex[x, y];
            if (currentPlate < 0 || currentPlate >= plateTypes.Length) continue;

            foreach (var (dx, dy) in neighbors)
            {
                var nx = GetWrappedX(worldWidth, x + dx);
                var ny = y + dy;

                if (ny < 0 || ny >= worldHeight) continue;

                var neighborPlate = plateIndex[nx, ny];
                if (neighborPlate == currentPlate) continue;

                if (neighborPlate < 0 || neighborPlate >= plateTypes.Length) continue;

                surfaceMap[x, y] = SurfaceFeature.Mountain;
            }
        }
    }

    // TODO: Didn't we calculate hotspot centers already?

    /// <summary>
    ///     Finds center points of strong hot spots.
    /// </summary>
    /// <param name="worldWidth">Width of the world in grid cells.</param>
    /// <param name="worldHeight">Height of the world in grid cells.</param>
    /// <param name="hotspotMap">Map hotspot strength per cell.</param>
    /// <returns></returns>
    private static List<(int x, int y, float strength)> FindHotspotCenters(
        int worldWidth,
        int worldHeight,
        in float[,] hotspotMap)
    {
        var centers = new List<(int, int, float)>();
        var visited = new bool[worldWidth, worldHeight];

        for (var y = 0; y < worldHeight; y++)
        for (var x = 0; x < worldWidth; x++)
        {
            if (visited[x, y] || hotspotMap[x, y] < HotspotMinStrength) continue;

            // Find local maximum
            var maxStrength = hotspotMap[x, y];
            var maxX = x;
            var maxY = y;

            // Search in a small radius for the actual peak
            for (var dy = -3; dy <= 3; dy++)
            for (var dx = -3; dx <= 3; dx++)
            {
                var nx = GetWrappedX(worldWidth, x + dx);
                var ny = y + dy;
                if (ny < 0 || ny >= worldHeight) continue;
                if (hotspotMap[nx, ny] <= maxStrength) continue;

                maxStrength = hotspotMap[nx, ny];
                maxX = nx;
                maxY = ny;
            }

            // Mark area around peak as visited
            for (var dy = -5; dy <= 5; dy++)
            for (var dx = -5; dx <= 5; dx++)
            {
                var nx = GetWrappedX(worldWidth, maxX + dx);
                var ny = maxY + dy;
                if (ny >= 0 && ny < worldHeight)
                    visited[nx, ny] = true;
            }

            centers.Add((maxX, maxY, maxStrength));
        }

        return centers;
    }

    private static void AddVolcanicDetails(int worldWidth, int worldHeight, float[,] elevations,
        SurfaceFeature[,] surfaceMap, int centerX, int centerY, float strength)
    {
        var radius = Math.Max(3, (int)(strength * 10)); // Scale radius with strength, minimum 3

        // Determine volcano type based on strength and characteristics
        var volcanoType = strength switch
        {
            > 0.9f => SurfaceFeature.Stratovolcano,
            > 0.6f => SurfaceFeature.Shield,
            _ => SurfaceFeature.Cinder
        };

        // Add crater at the peak for stratovolcanoes and large shields
        if (centerX >= 0 && centerX < worldWidth && centerY >= 0 && centerY < worldHeight)
        {
            var elevation = elevations[centerX, centerY];
            surfaceMap[centerX, centerY] = elevation switch
            {
                >= CraterElevationThreshold when volcanoType == SurfaceFeature.Stratovolcano ||
                                                 (volcanoType == SurfaceFeature.Shield &&
                                                  strength > 0.8f) => SurfaceFeature.Crater,
                >= CinderElevationThreshold when volcanoType == SurfaceFeature.Cinder =>
                    SurfaceFeature.Cinder,
                _ => surfaceMap[centerX, centerY]
            };
        }

        // Add volcanic features based on volcano type
        for (var y = Math.Max(0, centerY - radius);
             y < Math.Min(worldHeight, centerY + radius);
             y++)
        for (var x = centerX - radius; x < centerX + radius; x++)
        {
            var wrappedX = GetWrappedX(worldWidth, x);
            var distance = GetCylindricalDistance(worldWidth, wrappedX, y, centerX, centerY);

            if (distance > radius) continue;

            var normalizedDist = distance / radius;
            var elevation = elevations[wrappedX, y];

            // Skip if already has strong features
            var existing = surfaceMap[wrappedX, y];
            if (existing is SurfaceFeature.River or SurfaceFeature.Glacier or SurfaceFeature.Lava)
                continue;

            surfaceMap[wrappedX, y] = volcanoType switch
            {
                SurfaceFeature.Stratovolcano => normalizedDist switch
                {
                    // Stratovolcanoes: steep, explosive, with ash and lava
                    < 0.4f when elevation >= CalderaElevationThreshold => SurfaceFeature
                        .Stratovolcano,
                    > 0.3f and < 0.8f when elevation >= 5 => SurfaceFeature.Ash,
                    _ => surfaceMap[wrappedX, y]
                },
                SurfaceFeature.Shield => normalizedDist switch
                {
                    // Shield volcanoes: broad, gentle slopes, mostly lava
                    < 0.6f when elevation >= ShieldVolcanoThreshold => SurfaceFeature.Shield,
                    > 0.5f when elevation >= 4 => SurfaceFeature.Lava,
                    _ => surfaceMap[wrappedX, y]
                },
                _ => normalizedDist switch
                {
                    // Cinder cones: small, steep, cinder and ash
                    < 0.5f when elevation >= 4 => SurfaceFeature.Cinder,
                    > 0.4f when elevation >= 3 => SurfaceFeature.Ash,
                    _ => surfaceMap[wrappedX, y]
                }
            };

            // Add caldera for large stratovolcanoes at the center
            if (normalizedDist < 0.2f && volcanoType == SurfaceFeature.Stratovolcano &&
                strength > 0.9f && elevation >= 7) surfaceMap[wrappedX, y] = SurfaceFeature.Caldera;
        }
    }

    #endregion

    #region Coastal Features

    /// <summary>
    ///     Detect coastal features depending on their surrounding: beaches, cliffs and fjords.
    /// </summary>
    /// <param name="worldWidth">Width of the world in cells.</param>
    /// <param name="worldHeight">Height of the world in cells.</param>
    /// <param name="elevations">Elevation map of the world.</param>
    /// <param name="surfaceMap">Surface feature map of the world.</param>
    private static void ApplyCoastalFeatures(
        int worldWidth,
        int worldHeight,
        in float[,] elevations,
        in SurfaceFeature[,] surfaceMap)
    {
        for (var y = 0; y < worldHeight; y++)
        for (var x = 0; x < worldWidth; x++)
        {
            // Skip existing assigned strong surface features: river, lava, glacier
            var existing = surfaceMap[x, y];
            if (existing is SurfaceFeature.River or SurfaceFeature.Lava or SurfaceFeature.Glacier)
                continue;

            var elevation = elevations[x, y];
            var isWater = elevation <= SeaLevelElevation;

            // Beach/cliff only for land cells near water
            if (!isWater)
            {
                var adjacentWater = 0;
                var maxAdjElevation = 0f;
                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var nx = GetWrappedX(worldWidth, x + dx);
                    var ny = y + dy;
                    if (ny < 0 || ny >= worldHeight) continue;

                    var neighborElevation = elevations[nx, ny];
                    if (neighborElevation <= SeaLevelElevation)
                        adjacentWater++;
                    else
                        maxAdjElevation = Math.Max(maxAdjElevation, neighborElevation);
                }

                if (adjacentWater > 0)
                    surfaceMap[x, y] = elevation switch
                    {
                        // steep cliff if high land adjacent to water and steep drops nearby
                        >= HighMountainThreshold - 1 when maxAdjElevation <= SeaLevelElevation =>
                            SurfaceFeature.Cliff,
                        <= SnowThreshold => SurfaceFeature.Beach,
                        _ => SurfaceFeature.Cliff
                    };
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
                    var nx = GetWrappedX(worldWidth, x + dx);
                    var ny = y + dy;
                    if (ny < 0 || ny >= worldHeight) continue;
                    var neighborElevation = elevations[nx, ny];
                    if (neighborElevation < LandElevationThreshold) continue;
                    adjacentLand++;
                    if (neighborElevation >= SnowThreshold - 1)
                        adjacentHighMountain++;
                }

                if (adjacentLand >= 3 && adjacentHighMountain >= 1)
                    surfaceMap[x, y] = SurfaceFeature.Fjord;
            }
        }
    }

    #endregion

    #region Cylindrical Coordinate System

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
    private static float GetCylindricalDistance(int worldWidth, int x1, int y1, int x2, int y2)
    {
        var dx = Math.Min(Math.Abs(x2 - x1), worldWidth - Math.Abs(x2 - x1));
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
    private static float GetCylindricalDistance(int worldWidth, double x1, double y1, double x2,
        double y2)
    {
        var dx = Math.Min(Math.Abs(x2 - x1), worldWidth - Math.Abs(x2 - x1));
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
    private static float GetCylindricalDistanceSq(
        int worldWidth,
        double x1,
        double y1,
        double x2,
        double y2)
    {
        var dx = Math.Min(Math.Abs(x2 - x1), worldWidth - Math.Abs(x2 - x1));
        var dy = Math.Abs(y2 - y1);
        return (float)(dx * dx + dy * dy);
    }

    #endregion
}