using System.Buffers;
using System.Numerics;
using TermRTS.Examples.Greenery.Ecs.Component;

namespace TermRTS.Examples.Greenery.WorldGen;

// Refer to link below for a nice layered noise map implementation:
// https://github.com/SebLague/Procedural-Landmass-Generation/blob/master/Proc%20Gen%20E03/Assets/Scripts/Noise.cs

// TODO: Extract chunk classes into another file.

public enum SurfaceFeature : byte
{
    None,
    River,
    Glacier,
    Lava,
    Mountain,
    Snow,
    Beach,
    Cliff,
    Fjord,
    Crater,
    Ash,
    Cinder,
    Caldera,
    Shield,
    Stratovolcano
}

public enum Biome : byte
{
    // Water and Ice
    Ocean,
    IceCap,
    PolarDesert,
    Glacier,

    // Frost
    RockPeak,
    AlpineTundra,
    Tundra,
    SnowyForest,
    Taiga,

    // Temperate
    ColdDesert,
    HighlandMoor,
    Steppe,
    Grassland,
    TemperateForest,

    // Tropical
    CloudForest,
    HotDesert,
    Savanna,
    TropicalSeasonalForest,
    TropicalRainforest,

    // Rivers
    Creek,
    MinorRiver,
    MajorRiver
}

public sealed class WorldGenerationResult(
    WorldElevationChunk[] elevation,
    WorldSurfaceFeatureChunk[] surface,
    WorldTemperatureChunk[] temperature,
    WorldHumidityChunk[] humidity,
    WorldTemperatureAmplitudeChunk[] temperatureAmplitude,
    WorldBiomeChunk[] biome,
    WorldRiverChunk[] river)
{
    public WorldElevationChunk[] ElevationChunk { get; } = elevation;
    public WorldSurfaceFeatureChunk[] SurfaceFeatureChunk { get; } = surface;
    public WorldTemperatureChunk[] TemperatureChunk { get; } = temperature;
    public WorldHumidityChunk[] HumidityChunk { get; } = humidity;

    public WorldTemperatureAmplitudeChunk[] TemperatureAmplitudeChunk { get; } =
        temperatureAmplitude;

    public WorldBiomeChunk[] BiomeChunk { get; } = biome;
    public WorldRiverChunk[] RiverChunk { get; } = river;
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
    private const int MaxElevation = 9;
    private const int LandElevationThreshold = 4;
    private const int SeaLevelElevation = 3;
    private const int HighMountainThreshold = 7;
    private const int SnowThreshold = 8;

    // Constants for coastal slopes
    private const float MaxCoastalSlope = 5.0f;

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
    private const float BaseTempMax = 35.0f;
    private const float BaseTempMin = -40.0f;
    private const float AridityConstant = 0.005f;
    private const float BaseTemperatureAmplitude = 10.0f;
    private const float LatitudeAmplitudeModifier = 20.0f;

    // Constants for river carving
    private const int RiverCarveMinElevation = 4;

    #endregion

    #region Fields

    private readonly Random _rng;
    private readonly int _seed;
    private readonly int _worldWidth;
    private readonly int _worldHeight;
    private readonly int _plateCount;

    private float _maxElevation = float.MinValue;
    private float _minTectonicDelta = float.MaxValue;
    private float _maxTectonicDelta = float.MinValue;
    private float _minHotspotHeight = float.MaxValue;
    private float _maxHotspotHeight = float.MinValue;

    // TODO: Distinguish between voronoi cells and tectonic plates
    // private readonly WorldBuffer<byte> _elevation;
    private readonly WorldBuffer<float> _elevation;
    private readonly WorldBuffer<int> _landWaterMap;
    private readonly WorldBuffer<(int, int)> _voronoiCells;
    private readonly WorldBuffer<bool> _voronoiCellTypes;
    private readonly WorldBuffer<int> _voronoiCellIndex;
    private readonly WorldBuffer<(int, int)> _plateCells;
    private readonly WorldBuffer<bool> _plateTypes;
    private readonly WorldBuffer<int> _plateIndex;
    private readonly WorldBuffer<Vector2> _plateMotions;
    private readonly WorldBuffer<float> _coastalSlopes;
    private readonly WorldBuffer<float> _tectonicDelta;
    private readonly WorldBuffer<float> _hotspotMap;
    private readonly WorldBuffer<float> _noiseMap;
    private readonly WorldBuffer<float> _temperature;
    private readonly WorldBuffer<float> _temperatureAmplitude;
    private readonly WorldBuffer<float> _humidity;
    private readonly WorldBuffer<float> _distToWaterMap;
    private readonly WorldBuffer<Biome> _biomes;

    private readonly WorldBuffer<SurfaceFeature> _surfaceFeatures;

    // TODO: Merge surface features and rivers
    private readonly WorldBuffer<bool> _riverMap;
    private readonly WorldBuffer<int> _strahlerRiver;

    #endregion

    #region Properties

    public int VoronoiCellCount { get; set; }

    public float LandRatio { get; set; }

    // River tuning parameters (adjust at runtime)
    // Lower thresholds create more rivers; higher thresholds make rivers rarer.

    // normalized flow-level for river initiation
    public float RiverFormationThreshold { get; set; } = 0.01f;

    public float RiverCarveScale { get; set; } = 0.01f; // scaling factor for river depth from flow
    public float RiverMaxCarveDepth { get; set; } = 3.0f; // maximum depth a river can carve

    // Rainfall tuning parameters (used in river generation)
    // search radius to nearest water for rainfall boost
    public int RainfallWaterDistanceRadius { get; set; } = 2;

    public float RainfallWaterDistancePenalty { get; set; } = 0.07f; // weight for distance penalty
    public float RainfallMinValue { get; set; } = 0.0f; // minimum rainfall on land

    // how quickly rainfall falls with elevation
    public float RainfallElevationDecay { get; set; } = 0.1f;

    public float RainfallMinModifier { get; set; } = 0.2f; // minimum modifier due to elevation

    // Island chain (hotspot) tuning parameters
    public int MinIslandChains { get; set; } = 8; // minimum number of island chains
    public int MaxIslandChains { get; set; } = 15; // maximum number of island chains
    public int MinChainLength { get; set; } = 3; // minimum hotspots per chain
    public int MaxChainLength { get; set; } = 8; // maximum hotspots per chain
    public int ChainSpacing { get; set; } = 5; // spacing between hotspots in a chain

    public int MinHotspotRadius { get; set; } = 3; // minimum radius of volcanic cones
    public int MaxHotspotRadius { get; set; } = 8; // maximum radius of volcanic cones
    public float MinHotspotStrength { get; set; } = 3.4f; // minimum elevation strength of hotspots
    public float MaxHotspotStrength { get; set; } = 6.1f; // maximum elevation strength of hotspots

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
        float landRatio,
        int seed,
        int voronoiCellCount,
        int plateCount)
    {
        // init readonly fields
        _rng = new Random(seed);
        _seed = seed;
        _worldWidth = worldWidth;
        _worldHeight = worldHeight;
        LandRatio = landRatio;
        _plateCount = plateCount;

        // init private fields
        _elevation = new WorldBuffer<float>(worldWidth * worldHeight);
        _landWaterMap = new WorldBuffer<int>(voronoiCellCount);
        _voronoiCells = new WorldBuffer<(int, int)>(voronoiCellCount);
        _voronoiCellTypes = new WorldBuffer<bool>(voronoiCellCount);
        _voronoiCellIndex = new WorldBuffer<int>(worldWidth * worldHeight);
        _plateCells = new WorldBuffer<(int, int)>(plateCount);
        _plateTypes = new WorldBuffer<bool>(plateCount);
        _plateIndex = new WorldBuffer<int>(voronoiCellCount);
        _plateMotions = new WorldBuffer<Vector2>(voronoiCellCount);
        _coastalSlopes = new WorldBuffer<float>(worldWidth * worldHeight);
        _tectonicDelta = new WorldBuffer<float>(worldWidth * worldHeight);
        _hotspotMap = new WorldBuffer<float>(worldWidth * worldHeight);
        _noiseMap = new WorldBuffer<float>(worldWidth * worldHeight);
        _temperature = new WorldBuffer<float>(worldWidth * worldHeight);
        _temperatureAmplitude = new WorldBuffer<float>(worldWidth * worldHeight);
        _humidity = new WorldBuffer<float>(worldWidth * worldHeight);
        _distToWaterMap = new WorldBuffer<float>(_worldWidth * _worldHeight);
        _biomes = new WorldBuffer<Biome>(worldWidth * worldHeight);
        _surfaceFeatures = new WorldBuffer<SurfaceFeature>(worldWidth * worldHeight);
        _riverMap = new WorldBuffer<bool>(worldWidth * worldHeight);
        _strahlerRiver = new WorldBuffer<int>(worldWidth * worldHeight);

        // init properties
        VoronoiCellCount = voronoiCellCount;
    }

    #endregion

    #region IWorldGen Members

    public WorldGenerationResult Generate()
    {
        ValidateParameters();

        // TODO: Then reactivate step by step.
        // TODO: Find appropriate visualisations per step to examine visually.
        // TODO: Verify world generation works!
        // TODO: Optimize data structures and copying, streamline pipeline.
        // TODO: Decide on final world data necessary for game and visualisation.
        // TODO: Final optimisation of data structure use.

        // STAGE 1: Voronoi Cells and Land/Water distribution /////////////////////////////////////
        // Associate each grid cell to one of the voronoi cells.
        InitializeVoronoiCells();
        GenerateNoiseMap();
        GenerateLandWaterDistribution();
        // Stage 2: Plate Tectonics ///////////////////////////////////////////////////////////////
        InitializePlateTectonics();
        // Generate coastal slopes for each voronoi cell.
        GenerateSlopedCoasts();
        // Compute plate tectonics influence (mountains/trenches along plate boundaries).
        ComputePlateTectonicHeight();
        // Generate hotspots (mantle plumes creating volcanic islands/seamounts).
        GenerateHotspots();
        // For each voronoi land cell, apply perlin or simplex noise to generate height.
        ApplyNoiseAndSlopes();
        CalculateDistanceToWaterMap();
        // Generate climate (temperature, humidity, biomes, seasonal effects)
        GenerateClimate(true);

        // Apply erosion to smooth terrain and create realistic features
        // if (UseAdvancedErosion)
        ApplyAdvancedErosion();
        // else
        // ApplyErosion();

        // Generate rivers based on rainfall and elevation (tunable via public properties)
        GenerateRivers();

        // TODO: Re-generate distance to water?
        // Re-generate climate, now that we have rivers:
        GenerateClimate(false);

        // Apply mountain details (ridges, snow, glacier, lava)
        ApplyMountainDetails();

        // Apply coastal features (beach, cliff, fjord)
        ApplyCoastalFeatures();

        return new WorldGenerationResult(
            ToElevationChunks(),
            ToSurfaceFeatureChunks(),
            ToTemperatureChunks(),
            ToHumidityChunks(),
            ToTemperatureAmplitudeChunks(),
            ToBiomeChunks(),
            ToRiverChunks()
        );
    }

    /// <summary>
    /// Clear all world generation memory to avoid garbage.
    /// Only really necessary if some world generation steps are skipped.
    /// </summary>
    public void Reset()
    {
        _elevation.Memory.Span.Clear();
        _landWaterMap.Memory.Span.Clear();
        _voronoiCells.Memory.Span.Clear();
        _voronoiCellTypes.Memory.Span.Clear();
        _voronoiCellIndex.Memory.Span.Clear();
        _plateTypes.Memory.Span.Clear();
        _plateMotions.Memory.Span.Clear();
        _temperature.Memory.Span.Clear();
        _temperatureAmplitude.Memory.Span.Clear();
        _humidity.Memory.Span.Clear();
        _surfaceFeatures.Memory.Span.Clear();
        _riverMap.Memory.Span.Clear();
        _hotspotMap.Memory.Span.Clear();
        _tectonicDelta.Memory.Span.Clear();
        _coastalSlopes.Memory.Span.Clear();
        _noiseMap.Memory.Span.Clear();
        _strahlerRiver.Memory.Span.Clear();
    }

    #endregion

    #region Validation

    private void ValidateParameters()
    {
        if (_worldWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(_worldWidth),
                "World width must be greater than 0.");

        if (_worldHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(_worldHeight),
                "World height must be greater than 0.");

        if (LandRatio is < 0.0f or > 1.0f)
            throw new ArgumentOutOfRangeException(nameof(LandRatio),
                "Land ratio must be between 0 and 1.");
    }

    #endregion

    #region Noise Map

    /// <summary>
    ///     Generates a map of pseudo-random Perlin noise.
    /// </summary>
    private void GenerateNoiseMap()
    {
        var noise = new FastNoiseLite(_seed);
        noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        noise.SetFractalType(FastNoiseLite.FractalType.FBm);
        noise.SetFractalOctaves(4);
        noise.SetFractalLacunarity(3.0f);
        noise.SetFractalGain(0.5f);
        noise.SetFrequency(0.04f);

        var noiseMap = _noiseMap.Memory.Span;
        // We calculate a radius that keeps the scale consistent.
        // A larger radius results in more "zoomed in" noise.
        var radius = _worldWidth / (2.0f * (float)Math.PI);

        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
            {
                // Map X to a circle in 3D space
                var angle = x / (float)_worldWidth * 2.0f * (float)Math.PI;
                var nx = (float)Math.Cos(angle) * radius;
                var ny = (float)Math.Sin(angle) * radius;

                // Map Y to the vertical axis (Z)
                var nz = y;

                // Get noise [1, 1] and normalise to [0, 1]
                var noiseVal = noise.GetNoise(nx, ny, nz);
                noiseMap[y * _worldWidth + x] = (noiseVal + 1.0f) / 2.0f;
            }
    }

    private void ApplyNoiseAndSlopes()
    {
        var elevations = _elevation.Memory.Span;
        var coastalSlopes = _coastalSlopes.Memory.Span;
        var noiseField = _noiseMap.Memory.Span;
        var tectonicDelta = _tectonicDelta.Memory.Span;
        var hotspots = _hotspotMap.Memory.Span;

        var max = float.MinValue;
        var min = float.MaxValue;

        for (var i = 0; i < _worldWidth * _worldHeight; i += 1)
        {
            max = Math.Max(max, hotspots[i]);
            min = Math.Min(min, hotspots[i]);
        }


        // Apply noise and slopes to elevation map.
        for (var i = 0; i < _worldWidth * _worldHeight; i += 1)
        {
            // Continental plates (>=4) get higher base, oceanic (<4) get lower for deep trenches.
            var slopeFactor = coastalSlopes[i] / MaxCoastalSlope;
            var normalizedNoise = noiseField[i];
            var tectonicD = tectonicDelta[i];
            var hotspot = hotspots[i];

            // For oceanic plates, reduce noise impact to allow deeper trenches
            // var cellElevationContribution = elevations[i] >= LandElevationThreshold
            //     ? elevations[i]
            //     : 0;
            var noiseMultiplier = elevations[i] >= LandElevationThreshold
                ? 1.0f
                : -1.0f;

            var elevation = // cellElevationContribution +
                elevations[i] +
                slopeFactor *
                normalizedNoise * noiseMultiplier * elevations[i]; // *
            // var tectonic = (MaxElevation - elevation) * (tectonicD / _maxTectonicDelta);
            elevation = Math.Min(MaxElevation, elevation + tectonicD + hotspot);
            // Only apply hotspots if max elevation is not exceeded.
            // This should not happen in most cases as hotspots are supposed to be generated
            // in oceanic tiles.
            // if (hotspot > 0 && elevation + hotspot < MaxElevation) elevation += hotspot;

            // Store as float, don't clamp yet
            elevations[i] = Math.Max(0, elevation);
        }
    }

    #endregion

    #region World Base Structure

    /// <summary>
    ///     Initializes Voronoi cells of the world.
    /// </summary>
    /// <returns>Voronoi cells and their types, plate motions and land/water distribution.</returns>
    private void InitializeVoronoiCells()
    {
        var vCells = _voronoiCells.Memory.Span;
        // step 1: randomly sample <cellCount> coordinates of the grid as voronoi cell seeds
        for (var i = 0; i < VoronoiCellCount; i += 1)
            vCells[i] = (_rng.Next(_worldWidth), _rng.Next(_worldHeight));

        GenerateVoronoiCellTypes();

        var landWater = _landWaterMap.Memory.Span;
        var pTypes = _voronoiCellTypes.Memory.Span;
        for (var i = 0; i < VoronoiCellCount; i += 1)
            // Lower oceanic plates to create deeper oceans
            landWater[i] = pTypes[i] ? LandElevationThreshold : SeaLevelElevation;
    }

    /// <summary>
    ///     Generates the type for each plate:
    ///     true == continental, false == oceanic.
    /// </summary>
    private void GenerateVoronoiCellTypes()
    {
        var pTypes = _voronoiCellTypes.Memory.Span;
        for (var i = 0; i < VoronoiCellCount; i += 1)
            pTypes[i] = _rng.NextDouble() < LandRatio;
    }

    /// <summary>
    ///     Assigns an elevation and voronoi cell to each single cell on the map.
    /// </summary>
    private void GenerateLandWaterDistribution()
    {
        var noiseMap = _noiseMap.Memory.Span;
        const int jiggle = 80;
        var vCells = _voronoiCells.Memory.Span;
        var vIdx = _voronoiCellIndex.Memory.Span;
        var elevations = _elevation.Memory.Span;
        var landWater = _landWaterMap.Memory.Span;


        for (var y = 0; y < _worldHeight; y += 1)
            for (var x = 0; x < _worldWidth; x += 1)
            {
                var jiggleNoise = noiseMap[y * _worldWidth + x];
                var jiggledX = x + (0.5f - jiggleNoise) * jiggle;
                var jiggledY = y + (0.5f - jiggleNoise) * jiggle;

                var minDistSq = double.MaxValue;
                var winnerCell = 0;
                for (var i = 0; i < VoronoiCellCount; i += 1)
                {
                    var vX = vCells[i].Item1;
                    var vY = vCells[i].Item2;
                    var distSq = WorldMath.GetCylindricalDistanceSq(
                        jiggledX,
                        jiggledY,
                        vX,
                        vY);

                    if (distSq >= minDistSq) continue;
                    minDistSq = distSq;
                    winnerCell = i;
                }

                vIdx[y * _worldWidth + x] = winnerCell;
                elevations[y * _worldWidth + x] = Convert.ToByte(landWater[winnerCell]);
            }
    }

    #endregion

    #region Tectonics

    // TODO: Move this where it makes sense.
    private Vector2 GetWrappedVector(Vector2 from, Vector2 to)
    {
        return GetWrappedVector(to.X - from.X, to.Y - from.Y);
    }

    private Vector2 GetWrappedVector((int, int) from, (int, int) to)
    {
        return GetWrappedVector(to.Item1 - from.Item1, to.Item2 - from.Item2);
    }

    private Vector2 GetWrappedVector(float dx, float dy)
    {
        // If the distance is more than half the map, wrapping around is shorter
        if (MathF.Abs(dx) > _worldWidth / 2f) dx -= MathF.Sign(dx) * _worldWidth;
        return new Vector2(dx, dy);
    }

    /// <summary>
    ///     Initializes plates and tectonic parameters.
    /// </summary>
    private void InitializePlateTectonics()
    {
        var voronoiCells = _voronoiCells.Memory.Span;
        var plateCells = _plateCells.Memory.Span;
        var plateIndex = _plateIndex.Memory.Span;

        // step 1: randomly sample voronoi cells of the grid as plate seeds
        var plateTypes = _plateTypes.Memory.Span;
        var voronoiTypes = _voronoiCellTypes.Memory.Span;
        for (var i = 0; i < _plateCount; i += 1)
        {
            var voronoiIndex = _rng.Next(VoronoiCellCount);
            var voronoiCell = voronoiCells[voronoiIndex];
            plateCells[i] = (voronoiCell.Item1, voronoiCell.Item2);
            plateTypes[i] = voronoiTypes[voronoiIndex];
        }

        // step 2: assign voronoi cells to each plate
        for (var j = 0; j < VoronoiCellCount; j += 1)
        {
            var minDistSq = double.MaxValue;
            var winnerCell = 0;
            var (vX, vY) = voronoiCells[j];

            for (var i = 0; i < _plateCount; i += 1)
            {
                var (pX, pY) = plateCells[i];
                var distSq = WorldMath.GetCylindricalDistanceSq(vX, vY, pX, pY);

                if (distSq >= minDistSq) continue;
                minDistSq = distSq;
                winnerCell = i;
            }

            plateIndex[j] = winnerCell;
        }

        // step 2: assign plate motions and infer plate types from seed cells
        GeneratePlateMotions();
    }

    /// <summary>
    ///     Generates a motion vector for each plate.
    /// </summary>
    private void GeneratePlateMotions()
    {
        var motions = _plateMotions.Memory.Span;
        for (var i = 0; i < _plateCount; i += 1)
        {
            var angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
            // TODO: Play around with the speed formula.
            var speed = (float)_rng.NextDouble(); // * 0.5 + 0.1);
            motions[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;
        }
    }

    /// <summary>
    ///     Generates a gentle fall-off towards costal cells (elevation == 4)
    /// </summary>
    private void GenerateSlopedCoasts()
    {
        // 1 Initialize all cells to 9; then set boundary (land adjacent to water) to 0 and enqueue.
        var coastalSlopes = _coastalSlopes.Memory.Span;
        var elevations = _elevation.Memory.Span;
        for (var i = 0; i < _worldWidth * _worldHeight; i += 1)
            coastalSlopes[i] = MaxCoastalSlope;

        var q = new Queue<(int, int)>(_worldWidth * _worldHeight);
        const float tolerance = 0.5f;
        for (var y = 1; y < _worldHeight - 1; y += 1)
            for (var x = 1; x < _worldWidth - 1; x += 1)
            {
                if (Math.Abs(elevations[y * _worldWidth + x] - LandElevationThreshold) >
                    tolerance) continue;

                // Must have at least one water cell in its neighbourhood.
                if (Math.Abs(elevations[(y - 1) * _worldWidth + x] - SeaLevelElevation) >
                    tolerance && // north
                    Math.Abs(elevations[y * _worldWidth + x + 1] - SeaLevelElevation) >
                    tolerance && // east
                    Math.Abs(elevations[(y + 1) * _worldWidth + x] - SeaLevelElevation) >
                    tolerance && // south
                    Math.Abs(elevations[y * _worldWidth + (x - 1)] - SeaLevelElevation) > tolerance)
                    continue; // west

                coastalSlopes[y * _worldWidth + x] = 0.0f;
                q.Enqueue((x, y));
            }

        // 2 BFS: while queue is NOT empty, dequeue cell C and set neighbours to Min(C.elevation + 1, N.elevation)
        //                               enqueue all neighbors with updated elevation
        (int, int)[] directions = [(0, -1), (1, 0), (0, 1), (-1, 0)];
        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            var elevation = coastalSlopes[y * _worldWidth + x];
            foreach (var (dirX, dirY) in directions)
            {
                var neighX = WorldMath.WrapX(x + dirX);
                var neighY = y + dirY;

                if (neighY < 0
                    || neighY >= _worldHeight
                    || coastalSlopes[neighY * _worldWidth + neighX] <= elevation) continue;

                coastalSlopes[neighY * _worldWidth + neighX] = Math.Min(elevation + 1, 9);

                if (elevation < 8)
                    q.Enqueue((neighX, neighY));
            }
        }
    }

    /// <summary>
    ///     Generate elevation differences at tectonic plate boundaries,
    ///     e.g.: Mountains and seamounts at plate convergences and
    ///     trenches at plate divergences.
    ///     These changes will only affect world cells located at tectonic plate boundaries.
    /// </summary>
    private void ComputePlateTectonicHeight()
    {
        var tectonicDelta = _tectonicDelta.Memory.Span;
        var voronoiIndex = _voronoiCellIndex.Memory.Span;
        var plateMotions = _plateMotions.Memory.Span;
        var plateTypes = _plateTypes.Memory.Span;
        var plateCenters = _plateCells.Memory.Span;
        var plateIndex = _plateIndex.Memory.Span;

        var minContinentalConvergence = float.MaxValue;
        var maxContinentalConvergence = float.MinValue;
        var minMixedConvergence = float.MaxValue;
        var maxMixedConvergence = float.MinValue;
        var minOceanicConvergence = float.MaxValue;
        var maxOceanicConvergence = float.MinValue;
        var minOceanicDivergence = float.MaxValue;
        var maxOceanicDivergence = float.MinValue;
        var minOtherDivergence = float.MaxValue;
        var maxOtherDivergence = float.MinValue;

        for (var y = 0; y < _worldHeight; y += 1)
            for (var x = 0; x < _worldWidth; x += 1)
            {
                // TODO: Index plates directly on cells.
                var vIndex = voronoiIndex[y * _worldWidth + x];
                var currentPlate = plateIndex[vIndex];
                var totalDelta = 0f;

                (int, int)[] offsets = [(0, -1), (1, 0), (0, 1), (-1, 0)];
                foreach (var (dx, dy) in offsets)
                {
                    var nx = WorldMath.WrapX(x + dx);
                    var ny = y + dy;
                    if (ny < 0 || ny >= _worldHeight) continue;

                    var nvIndex = voronoiIndex[ny * _worldWidth + nx];
                    var neighbourPlate = plateIndex[nvIndex];
                    if (neighbourPlate == currentPlate) continue;

                    var pA = plateCenters[currentPlate];
                    var pB = plateCenters[neighbourPlate];
                    var direction = GetWrappedVector(pA, pB);
                    if (direction.LengthSquared() < 0.0001f) continue;

                    var normal = Vector2.Normalize(direction);
                    var relativeMotion = GetWrappedVector(plateMotions[neighbourPlate],
                        plateMotions[currentPlate]);
                    var stress = Vector2.Dot(relativeMotion, normal);
                    // Negative stress means they are crashing.
                    var convergence = MathF.Max(0f, -stress);
                    // Positive stress means they are pulling apart.
                    var divergence = MathF.Max(0f, stress);
                    // Only true if both are continents.
                    var continentalInteraction = plateTypes[currentPlate] && plateTypes[neighbourPlate];
                    // Only true if one plate is continental and the other is oceanic.
                    var mixedInteraction = plateTypes[currentPlate] ^ plateTypes[neighbourPlate];

                    if (convergence > 0)
                    {
                        if (continentalInteraction)
                        {
                            minContinentalConvergence =
                                MathF.Min(minContinentalConvergence, convergence * 250f);
                            maxContinentalConvergence =
                                MathF.Max(maxContinentalConvergence, convergence * 250f);
                            // Higher mountains for continental convergence.
                            // TODO: turn into adjustable parameter
                            totalDelta += convergence * 250f;
                        }
                        else if (mixedInteraction)
                        {
                            minMixedConvergence = MathF.Min(minMixedConvergence, convergence * 4.5f);
                            maxMixedConvergence = MathF.Max(maxMixedConvergence, convergence * 4.5f);
                            // Lesser mountains for mixed convergence.
                            // TODO: turn into adjustable parameter
                            totalDelta += convergence * 4.5f;
                        }
                        else
                        {
                            minOceanicConvergence =
                                MathF.Min(minOceanicConvergence, convergence * 1.2f);
                            maxOceanicConvergence =
                                MathF.Max(maxOceanicConvergence, convergence * 1.2f);
                            // Small bumps for oceanic convergence.
                            // TODO: Does this influence hotspots and hotspot island chains?
                            // TODO: turn into adjustable parameter
                            totalDelta += convergence * 1.2f;
                        }
                    }
                    else if (divergence > 0)
                    {
                        if (!plateTypes[currentPlate] && !plateTypes[neighbourPlate])
                        {
                            minOceanicDivergence = MathF.Min(minOceanicDivergence, divergence * 2f);
                            maxOceanicDivergence = MathF.Max(maxOceanicDivergence, divergence * 2f);
                            // Very deep trenches for oceanic-oceanic divergence.
                            // TODO: turn into adjustable parameter
                            totalDelta -= divergence * 2.0f;
                        }
                        else
                        {
                            minOtherDivergence = MathF.Min(minOtherDivergence, divergence * 2.1f);
                            maxOtherDivergence = MathF.Max(maxOtherDivergence, divergence * 2.1f);
                            // Shallower trenches for all other divergences
                            // TODO: turn into adjustable parameter
                            totalDelta -= divergence * 2.1f;
                        }
                    }
                }

                _minTectonicDelta = MathF.Min(totalDelta, _minTectonicDelta);
                _maxTectonicDelta = MathF.Max(totalDelta, _maxTectonicDelta);
                tectonicDelta[y * _worldWidth + x] = totalDelta;
            }

        Console.WriteLine($"");
    }

    // TODO: Turn numeric parameters in to constants and later tweakable Properties
    /// <summary>
    ///     Assign hotspots to certain cells in the world.
    /// </summary>
    private void GenerateHotspots()
    {
        var hotspots = _hotspotMap.Memory.Span;
        var voronoiTypes = _voronoiCellTypes.Memory.Span;
        var voronoiCenters = _voronoiCells.Memory.Span;
        var plateMotions = _plateMotions.Memory.Span;
        var noiseMap = _noiseMap.Memory.Span;
        // +1 because Next upper bound is exclusive
        var chainCount = _rng.Next(MinIslandChains, MaxIslandChains + 1);

        var oceanPlateIds = new List<int>();
        for (var i = 0; i < voronoiTypes.Length; i++)
            if (!voronoiTypes[i])
                oceanPlateIds.Add(i);

        for (var chain = 0; chain < chainCount; chain++)
        {
            if (oceanPlateIds.Count == 0) continue;

            var oceanPlateId = oceanPlateIds[_rng.Next(oceanPlateIds.Count)];
            oceanPlateIds.Remove(oceanPlateId);
            var (startX, startY) = voronoiCenters[oceanPlateId];

            // Create a chain of hotspots along a plate motion direction.
            var chainLength = _rng.Next(MinChainLength, MaxChainLength + 1);

            // Use a random plate motion direction for the chain, or create a random direction
            var chainDirection = plateMotions.Length > 0
                ? plateMotions[_rng.Next(plateMotions.Length)]
                : new Vector2(
                    (float)(_rng.NextDouble() - 0.5) * 2,
                    (float)(_rng.NextDouble() - 0.5) * 2);

            // Normalize and scale the direction
            var length = MathF.Sqrt(
                chainDirection.X * chainDirection.X +
                chainDirection.Y * chainDirection.Y);
            if (length > 0)
                chainDirection = chainDirection / length * (float)(_rng.NextDouble() * 2 + 1);

            for (var i = 0; i < chainLength; i++)
            {
                // Calculate position along the chain with configurable spacing
                var offsetX = (int)(chainDirection.X * i * ChainSpacing);
                var offsetY = (int)(chainDirection.Y * i * ChainSpacing);
                var centerX = WorldMath.WrapX(startX + offsetX);
                var centerY = startY + offsetY;

                // Keep within bounds with some wrapping
                // centerX = (centerX % worldWidth + worldWidth) % worldWidth;
                // centerY = (centerY % worldHeight + worldHeight) % worldHeight;
                if (centerY < 0 || centerY >= _worldHeight) continue;

                var radius = _rng.Next(MinHotspotRadius, MaxHotspotRadius + 1);
                var strength =
                    (float)(_rng.NextDouble() * (MaxHotspotStrength - MinHotspotStrength) +
                            MinHotspotStrength);

                // Create a volcanic cone shape with exponential falloff (more realistic)
                // TODO: This looks very inefficient.
                var minY = Math.Max(0, centerY - radius);
                var maxY = Math.Min(_worldHeight, centerY + radius);
                var minX = Math.Max(0, centerX - radius);
                var maxX = Math.Min(_worldWidth, centerX + radius);
                // In equatorial latitudes 50% chance to generate an atoll instead. 
                var isAtoll = MathF.Abs(_worldHeight / 2F - centerY) < _worldHeight / 3F &&
                              _rng.NextDouble() < 0.5F;
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
                        var noise = noiseMap[y * _worldWidth + x];
                        //Noise.CalcPixel2D(x * 20, y * 20, 0.05f) * 0.5f + 0.5f;
                        coneHeight *= 0.7f + noise * 0.6f;

                        if (isAtoll && hotspots[y * _worldWidth + x] + coneHeight >=
                            MaxHotspotStrength - 2)
                            hotspots[y * _worldWidth + x] = 0;
                        else
                            hotspots[y * _worldWidth + x] += coneHeight;

                        _minHotspotHeight = Math.Min(_minHotspotHeight, coneHeight);
                        _maxHotspotHeight = Math.Max(_maxHotspotHeight, coneHeight);
                    }
            }
        }
    }

    #endregion

    #region Climate and Biomes

    // TODO: Can we structure our world generation steps better than to use this bool flag?
    // TODO: Separate biomes out of this
    /// <summary>
    ///     Generates world maps for various climate features.
    /// </summary>
    private void GenerateClimate(bool changeHumidity)
    {
        var elevations = _elevation.Memory.Span;
        var temperature = _temperature.Memory.Span;
        var temperatureAmplitude = _temperatureAmplitude.Memory.Span;
        var humidity = _humidity.Memory.Span;
        var distToWaterMap = _distToWaterMap.Memory.Span;
        var strahlerRiver = _strahlerRiver.Memory.Span;
        var riverMap = _riverMap.Memory.Span;
        var biomes = _biomes.Memory.Span;

        for (var y = 0; y < _worldHeight; y++)
        {
            // Latitude factor: 0 at equator (middle), 1 at poles
            // 1. Calculate the raw 0 to 1 factor as before
            var rawLat = Math.Abs(y - _worldHeight / 2.0f) / (_worldHeight / 2.0f);

            // 2. Apply a shaping function to "stretch" the middle
            // Option A: The "Equator Stretch" (Pushes temperate zones toward poles)
            // var latitudeFactor = Math.Pow(rawLat, 1.5f); 

            // Option B: The "Sine Curve" (Most realistic distribution)
            // This makes the temperature drop-off very slow at the equator, 
            // faster in the mid-latitudes, and slow again at the poles.
            // var latitudeFactor = MathF.Sin((float)(rawLat * Math.PI / 2.0));
            var latitudeFactor = (float)Math.Pow(rawLat, 1.1);
            // Temperature: base -30 to 30, decreases with latitude and elevation
            var baseTemp = BaseTempMax - (BaseTempMax - BaseTempMin) * latitudeFactor;

            // Creates peaks of moisture at 0 and 60 degrees, and troughs at 30 and 90.
            var latRad = latitudeFactor * MathF.PI;
            var latMoistureMod = 0.5f + 0.5f * MathF.Cos(3 * latRad);
            var rowOffset = y * _worldWidth;

            for (var x = 0; x < _worldWidth; x++)
            {
                var idx = rowOffset + x;
                var elevation = elevations[idx];
                var elevationFactor = elevation / MaxElevation;
                var isWater = elevation <= 3;

                // Colder at higher elevation.
                temperature[idx] = baseTemp + elevationFactor * elevation;

                if (changeHumidity)
                {
                    // 1. Exponential decay from water
                    var dist = distToWaterMap[idx];
                    var humidityBase = MathF.Exp(-dist * AridityConstant);

                    // 2. Apply Latitude and Elevation penalties
                    // Higher elevations hold less total moisture (colder air)
                    var elevationPenalty = elevation * 0.02f;

                    var finalHumidity = humidityBase * latMoistureMod - elevationPenalty;

                    // 3. Add River Influence
                    // Rivers provide a small "humidity corridor" in dry areas
                    finalHumidity += strahlerRiver[idx] * 0.1f;

                    humidity[idx] = Math.Clamp(finalHumidity, 0.0f, 1.0f);
                }

                // Seasonal amplitude: larger at higher latitudes and higher elevations
                temperatureAmplitude[idx]
                    = BaseTemperatureAmplitude
                      + LatitudeAmplitudeModifier * latitudeFactor
                      + elevation * elevationFactor;


                // Calculate Relative Humidity
                // This scales Absolute Moisture by the air's carrying capacity at a given temperature.
                var possibleHumidityAtTemp = MathF.Min(0.1f, (temperature[idx] + 50f) / 80f);
                var relHumidity = Math.Clamp(humidity[idx] / possibleHumidityAtTemp, 0.0f, 1.0f);
                humidity[idx] = relHumidity;

                var isRiver = riverMap[idx];
                var strahler = strahlerRiver[idx];
                // Determine biome
                biomes[idx] = DetermineBiome(
                    temperature[idx],
                    relHumidity,
                    elevation,
                    isWater,
                    isRiver,
                    strahler);
            }
        }
    }

    /// <summary>
    ///     Calculates the distance from every land cell to the nearest water cell 
    ///     using an O(N) Breadth-First Search.
    /// </summary>
    private void CalculateDistanceToWaterMap()
    {
        var elevations = _elevation.Memory.Span;
        var distToWaterMap = _distToWaterMap.Memory.Span;
        var visited = new bool[_worldWidth, _worldHeight];
        var queue = new Queue<(int x, int y, int dist)>();

        // 1. Initialize: Find all water and add to queue
        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
            {
                var idx = y * _worldWidth + x;
                if (elevations[idx] <= SeaLevelElevation)
                {
                    distToWaterMap[idx] = 0;
                    visited[x, y] = true;
                    queue.Enqueue((x, y, 0));
                }
                else
                {
                    distToWaterMap[idx] = -1; // Mark land as uncalculated
                }
            }

        // Directions for 4-way connectivity (N, S, E, W)
        // Use 8-way if you want diagonal distances accounted for
        var directions = new (int dx, int dy)[] { (0, 1), (0, -1), (1, 0), (-1, 0) };

        // 2. Process the queue
        while (queue.Count > 0)
        {
            var (cx, cy, currentDist) = queue.Dequeue();

            foreach (var (dx, dy) in directions)
            {
                var nx = WorldMath.WrapX(cx + dx);
                var ny = cy + dy;

                // Check vertical bounds
                if (ny < 0 || ny >= _worldHeight) continue;

                // If not visited, it's the closest we've found so far
                if (visited[nx, ny]) continue;
                visited[nx, ny] = true;
                var nextDist = currentDist + 1;
                distToWaterMap[ny * _worldWidth + nx] = nextDist;

                // Optimization: You can stop the queue if nextDist > MaxSearchRadius
                // but usually, a full distance map is useful for AI city placement!
                queue.Enqueue((nx, ny, nextDist));
            }
        }
    }

    /// <summary>
    ///     Determine the biome of a cell by its properties.
    ///     Suggestion:
    ///     Moisture	Low Temp (Tundra)	Mid Temp (Temperate)	High Temp (Tropical)
    ///     Low	Ice / Polar Desert	Steppe / Cold Desert	Hot Desert
    ///     Mid	Shrubland	Grassland / Woodland	Savanna
    ///     High	Taiga (Boreal)	Seasonal Forest	Tropical Rainforest
    /// </summary>
    /// <param name="temp">Temperature of the cell.</param>
    /// <param name="relHumidity">Humidity of the cell.</param>
    /// <param name="elevation">Elevation of the cell.</param>
    /// <param name="isWater">True if the cell is sea, false otherwise.</param>
    /// <param name="strahlerOrder">Strahler order for river cells.</param>
    /// <returns>Biome of the cell.</returns>
    private static Biome DetermineBiome(
        float temp,
        float relHumidity,
        float elevation,
        bool isWater,
        bool isRiver,
        int strahlerOrder)
    {
        if (isWater) return Biome.Ocean;

        if (isRiver)
            return strahlerOrder switch
            {
                <= 2 => Biome.Creek,
                <= 4 => Biome.MinorRiver,
                _ => Biome.MajorRiver
            };


        // High Altitude "Dead Zone" (Above the Tree Line)
        if (elevation > HighMountainThreshold) return temp < 0f ? Biome.Glacier : Biome.RockPeak;

        switch (temp)
        {
            // 3. Extreme Cold (Polar / Arctic)
            case < -10f:
                // High humidity in extreme cold leads to permanent ice sheets/glaciers.
                // Low humidity leads to barren, frozen gravel/dust plains.
                return relHumidity < 0.3f ? Biome.PolarDesert : Biome.IceCap;
            // 4. Cold / Sub-Arctic (Boreal)
            case < 5f when relHumidity < 0.25f:
                return Biome.Tundra;
            // Use elevation for Alpine variations
            case < 5f when elevation > CinderElevationThreshold:
                return Biome.AlpineTundra;
            // Differentiate between standard Boreal forest and heavy precipitation zones
            case < 5f:
                return relHumidity > 0.75f ? Biome.SnowyForest : Biome.Taiga;
            // 5. Temperate
            case < 22f when relHumidity < 0.15f:
                return Biome.ColdDesert;
            case < 22f when elevation > CinderElevationThreshold && relHumidity > 0.5f:
                return Biome.HighlandMoor;
            case < 22f when relHumidity < 0.4f:
                return Biome.Steppe;
            case < 22f when relHumidity < 0.7f:
                return Biome.Grassland;
            case < 22f:
                return Biome.TemperateForest; // High humidity temperate zones
        }

        // 6. Tropical / Hot
        // High altitude tropics create unique "Cloud Forests" (very high humidity + altitude)
        if (elevation > CinderElevationThreshold && relHumidity > 0.8f)
            return Biome.CloudForest;

        return relHumidity switch
        {
            < 0.15f => Biome.HotDesert,
            < 0.45f => Biome.Savanna,
            < 0.8f => Biome.TropicalSeasonalForest,
            _ => Biome.TropicalRainforest
        };
    }

    #endregion

    #region Erosion

    // TODO: Clarify that this meets the definition of thermal erosion.
    // TODO: Possibly delete in favour of advanced erosion!
    /// <summary>
    ///     Apply thermal and hydraulic erosion to
    /// </summary>
    private void ApplyErosion()
    {
        var elevationsF = _elevation.Memory.Span;
        var elevationBuf = new Span<float>(new float[_worldWidth * _worldHeight]);
        for (var i = 0; i < _worldWidth * _worldHeight; i++) elevationBuf[i] = elevationsF[i];

        var elevationSource = elevationsF;
        var elevationDest = elevationBuf;
        // Simple thermal erosion + hydraulic erosion simulation

        for (var iter = 0; iter < SimpleErosionIterations; iter++)
        {
            for (var y = 1; y < _worldHeight - 1; y++)
                for (var x = 0; x < _worldWidth; x++)
                {
                    var currentHeight = elevationSource[y * _worldWidth + x];
                    var lowestNeighbor = currentHeight;
                    var lowestX = x;
                    var lowestY = y;

                    // Find lowest neighbor
                    (int, int)[] directions =
                        [(0, -1), (1, 0), (0, 1), (-1, 0), (1, -1), (1, 1), (-1, 1), (-1, -1)];
                    foreach (var (dx, dy) in directions)
                    {
                        var nx = WorldMath.WrapX(x + dx);
                        var ny = y + dy;
                        var neighborHeight = elevationSource[ny * _worldWidth + nx];

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
                        elevationDest[y * _worldWidth + x] -= slideAmount;
                        elevationDest[lowestY * _worldWidth + lowestX] += slideAmount;
                    }

                    // Hydraulic erosion: water flow simulation (simplified)
                    // TODO: Possibly parameterize water surface level (=3)
                    var waterFlow = Math.Max(0, currentHeight - 3); // Water surface at 3
                    if (waterFlow <= 0) continue;

                    var erosionAmount = Math.Min(SimpleErosionRate * waterFlow * 0.1f, 0.5f);
                    elevationDest[y * _worldWidth + x] -= erosionAmount;
                }

            var tmp = elevationSource;
            elevationSource = elevationDest;
            elevationDest = tmp;
        }
    }

    /// <summary>
    ///     Apply erosion with hydraulic simulation, sediment transport and thermal erosion.
    /// </summary>
    private void ApplyAdvancedErosion()
    {
        // TODO: Borrow arrays!
        // Advanced erosion with hydraulic simulation, sediment transport, and thermal erosion
        var water = new float[_worldWidth * _worldHeight];
        var sediment = new float[_worldWidth * _worldHeight];
        var terrain = new float[_worldWidth * _worldHeight];

        var elevationsF = _elevation.Memory.Span;
        var biomes = _biomes.Memory.Span;
        var humidity = _humidity.Memory.Span;
        var hotspots = _hotspotMap.Memory.Span;

        // Copy elevations to terrain (already float)
        for (var i = 0; i < _worldHeight * _worldWidth; i++)
            terrain[i] = elevationsF[i];

        for (var iter = 0; iter < ErosionIterations; iter++)
        {
            // Step 1: Add rainfall (climate-aware)
            for (var i = 0; i < _worldWidth * _worldHeight; i++)
            {
                // TODO: Possibly turn localRainRate coefficients into constants or tweakable properties
                var localRainRate = RainRate;
                // Drier regions get less rainfall
                if (biomes[i] == Biome.HotDesert || biomes[i] == Biome.Tundra)
                    localRainRate *= 0.3f; // 70% less rain in dry/arid regions
                else if (humidity[i] < 0.3f)
                    localRainRate *= 0.5f; // 50% less rain in dry areas

                water[i] += localRainRate;
            }

            // Step 2: Water flow simulation
            var velocityX = new float[_worldWidth, _worldHeight];
            var velocityY = new float[_worldWidth, _worldHeight];

            // Calculate water flow directions and velocities
            // TODO: Make this work with wrapped X-coordinates.
            for (var y = 1; y < _worldHeight - 1; y++)
                for (var x = 1; x < _worldWidth - 1; x++)
                {
                    // Calculate slope in all directions
                    var slopeX =
                        (terrain[y * _worldWidth + (x - 1)] + terrain[y * _worldWidth + x + 1]) * 0.5f -
                        terrain[y * _worldWidth + x];
                    var slopeY =
                        (terrain[(y - 1) * _worldWidth + x] + terrain[(y + 1) * _worldWidth + x]) *
                        0.5f - terrain[y * _worldWidth + x];

                    var slope = MathF.Sqrt(slopeX * slopeX + slopeY * slopeY);
                    if (slope < MinSlope) continue;

                    // Calculate flow direction
                    var flowX = slopeX / slope;
                    var flowY = slopeY / slope;

                    // Calculate velocity based on slope and water depth
                    var velocity = MathF.Sqrt(Gravity * slope) * water[y * _worldWidth + x];
                    velocityX[x, y] = flowX * velocity;
                    velocityY[x, y] = flowY * velocity;
                }

            // Step 3: Hydraulic erosion and sediment transport
            var newTerrain = new float[_worldWidth * _worldHeight];
            terrain.CopyTo(newTerrain);
            var newWater = new float[_worldWidth * _worldHeight];
            water.CopyTo(newWater);
            var newSediment = new float[_worldWidth * _worldHeight];
            sediment.CopyTo(newSediment);

            for (var y = 1; y < _worldHeight - 1; y++)
                for (var x = 1; x < _worldWidth - 1; x++)
                {
                    var currentWater = water[y * _worldWidth + x];
                    if (currentWater <= 0) continue;

                    // Calculate sediment capacity based on water velocity
                    var velocity = MathF.Sqrt(velocityX[x, y] * velocityX[x, y] +
                                              velocityY[x, y] * velocityY[x, y]);
                    var capacity = SedimentCapacity * velocity * currentWater;

                    var currentSediment = sediment[y * _worldWidth + x];

                    if (currentSediment > capacity)
                    {
                        // Deposit sediment
                        var depositAmount = (currentSediment - capacity) * DepositionRate;
                        newTerrain[y * _worldWidth + x] += depositAmount;
                        newSediment[y * _worldWidth + x] -= depositAmount;
                    }
                    else
                    {
                        // Erode terrain (with volcanic resistance)
                        var erodeAmount = (capacity - currentSediment) * HydraulicErosionRate;

                        // Volcanic areas are much more resistant to erosion
                        var volcanicResistance = 1.0f;
                        if (hotspots[y * _worldWidth + x] > 0.1f) // Areas with volcanic activity
                                                                  // 90% less erosion in volcanic areas
                            volcanicResistance = VolcanicResistance;

                        erodeAmount *= volcanicResistance;
                        // Don't erode too much. // TODO: Do we really need this guard?
                        erodeAmount = Math.Min(erodeAmount, terrain[y * _worldWidth + x] * 0.1f);
                        newTerrain[y * _worldWidth + x] -= erodeAmount;
                        newSediment[y * _worldWidth + x] += erodeAmount;
                    }

                    // Water flow to neighboring cells
                    var totalOutflow = 0f;
                    (int, int)[] directions = [(0, -1), (1, 0), (0, 1), (-1, 0)];

                    foreach (var (dx, dy) in directions)
                    {
                        var nx = WorldMath.WrapX(x + dx);
                        var ny = y + dy;

                        if (ny < 0 || ny >= _worldHeight) continue;

                        var neighborSlope =
                            terrain[y * _worldWidth + x] - terrain[ny * _worldWidth + nx];
                        if (neighborSlope <= 0) continue;

                        var outflow = currentWater * neighborSlope * HydraulicErosionRate;
                        newWater[ny * _worldWidth + nx] += outflow * 0.25f; // Distribute to neighbors
                        newSediment[ny * _worldWidth + nx] += currentSediment * outflow /
                            Math.Max(currentWater, WaterViscosity) * 0.25f;
                        totalOutflow += outflow;
                    }

                    newWater[y * _worldWidth + x] -= totalOutflow;
                    newSediment[y * _worldWidth + x] -=
                        currentSediment * totalOutflow / Math.Max(currentWater, 0.001f);
                }

            // Step 4: Thermal erosion (material sliding)
            // TODO: Fix reading and writing form/to newTerrain in the same loop!
            // TODO: Make this work with wrapped x-coordinate.
            for (var y = 1; y < _worldHeight - 1; y++)
                for (var x = 1; x < _worldWidth - 1; x++)
                {
                    var currentHeight = newTerrain[y * _worldWidth + x];
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
                        var neighborHeight = newTerrain[ny * _worldWidth + nx];

                        if (neighborHeight >= lowestNeighbor) continue;
                        lowestNeighbor = neighborHeight;
                        lowestX = nx;
                        lowestY = ny;
                    }

                    var slope = currentHeight - lowestNeighbor;

                    // Material slides if slope is too steep
                    if (slope <= TalusAngle) continue;
                    var slideAmount = Math.Min(ThermalErosionRate * slope, slope * 0.5f);
                    newTerrain[y * _worldWidth + x] -= slideAmount;
                    newTerrain[lowestY * _worldWidth + lowestX] += slideAmount;
                }

            // Step 5: Evaporation (climate-aware)
            for (var y = 0; y < _worldHeight; y++)
                for (var x = 0; x < _worldWidth; x++)
                {
                    // Higher evaporation in dry/hot climates
                    var climateModifier = 1.0f;
                    var biome = biomes[y * _worldWidth + x];
                    var humidityValue = humidity[y * _worldWidth + x];

                    if (biome is Biome.HotDesert or Biome.ColdDesert or Biome.Tundra)
                        climateModifier = 2.0f; // 2x evaporation in dry areas
                    else if (humidityValue < 0.3f)
                        climateModifier = 1.5f; // 1.5x evaporation in low humidity areas

                    var adjustedEvaporation = EvaporationRate * climateModifier;
                    newWater[y * _worldWidth + x] =
                        Math.Max(0, newWater[y * _worldWidth + x] - adjustedEvaporation);
                }

            // Update arrays
            // TODO: Double buffer to swap between arrays.
            terrain = newTerrain;
            water = newWater;
            sediment = newSediment;
        }

        // Copy back to elevations (keep as float for now)
        for (var i = 0; i < _worldWidth * _worldHeight; i++)
            elevationsF[i] = terrain[i];
    }

    #endregion

    #region Rivers

    // TODO: Check whether to let this be influenced by biome, temperature or humidity as well.

    /// <summary>
    ///     Generates rivers on the map.
    /// </summary>
    private void GenerateRivers()
    {
        // Step 0: Fix local minima so water doesn't get stuck
        FillDepressions();

        // Step 1: Generate rainfall map
        var rainfall = GenerateRainfall();
        ApplyRainShadows(rainfall);

        // Step 2: Calculate flow directions (steepest downhill neighbor)
        var flowDirections = CalculateFlowDirections();

        // Step 3: Accumulate flow from upstream cells
        var flowAccumulation = AccumulateFlow(flowDirections, rainfall);
        CalculateStrahlerOrder(flowDirections);

        // Step 4: Carve rivers where flow accumulation is high enough
        CarveRivers(flowDirections, flowAccumulation);

        // Step 5: Deposit sediment in river valleys (optional)
        DepositSediment(flowAccumulation);

        GenerateMoistureMap(rainfall);
    }

    /// <summary>
    ///     Generate a map of rainfall per cell.
    ///     Rainfall increases with proximity to water and decreases with elevation.
    /// </summary>
    /// <returns>A grid of rain fall per cell.</returns>
    private float[,] GenerateRainfall()
    {
        var elevationsF = _elevation.Memory.Span;
        var distToWaterMap = _distToWaterMap.Memory.Span;
        var rainfall = new float[_worldWidth, _worldHeight];

        // Base rainfall increases with proximity to water and decreases with elevation
        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
            {
                var elevation = elevationsF[y * _worldWidth + x];
                var isWater = elevation <= SeaLevelElevation; // Water cells get maximum rainfall

                // Distance to nearest water (simplified - just check neighbors)
                var waterDistance = distToWaterMap[y * _worldWidth + x];

                // Rainfall formula: high near water, decreases with elevation and distance
                var baseRainfall = isWater
                    ? 1.0f
                    : MathF.Max(RainfallMinValue, 1.0f - waterDistance * RainfallWaterDistancePenalty);
                var elevationModifier = MathF.Max(RainfallMinModifier,
                    1.0f - elevation * RainfallElevationDecay);
                rainfall[x, y] = baseRainfall * elevationModifier;
            }

        return rainfall;
    }

    /// <summary>
    ///     Adjusts the rainfall map based on wind direction and terrain height.
    ///     Simulates moisture being 'squeezed' out of clouds by mountains.
    /// </summary>
    /// <summary>
    ///     Adjusts the rainfall map based on global wind bands (Trade Winds, Westerlies) 
    ///     and terrain height to simulate realistic rain shadows.
    /// </summary>
    private void ApplyRainShadows(in float[,] rainfall)
    {
        var elevationsF = _elevation.Memory.Span;

        const float moistureRechargeRate = 0.08f;
        const float rainDropFactor = 0.05f;
        const float baseCloudMoisture = 0.5f;

        for (var y = 0; y < _worldHeight; y++)
        {
            var cloudMoisture = baseCloudMoisture;

            // 1. Calculate Latitude (0.0 at the equator, 1.0 at the poles)
            // Assuming y=0 is the North Pole, and y=_worldHeight/2 is the Equator
            var latitude = MathF.Abs(y - _worldHeight / 2f) / (_worldHeight / 2f);

            // 2. Determine Wind Direction based on Global Bands
            // < 0.33f is Tropical (Trade Winds: East to West)
            // < 0.66f is Temperate (Westerlies: West to East)
            // >= 0.66f is Polar (Easterlies: East to West)
            var windDir = latitude < 0.33f || latitude >= 0.66f ? -1 : 1;

            // 3. Sweep across the map
            for (var step = 0; step < _worldWidth * 2; step++)
            {
                // If windDir is 1 (West->East), we read left to right.
                // If windDir is -1 (East->West), we read right to left.
                var logicalX = windDir == 1 ? step : -step;
                var windX = WorldMath.WrapX(logicalX);
                var index = y * _worldWidth + windX;
                var elevation = elevationsF[index];

                var isSecondPass = step >= _worldWidth;

                if (elevation <= SeaLevelElevation)
                {
                    // RECHARGE
                    // Inside the 'isWater' check of ApplyRainShadows
                    var tempAtTile = _temperature.Memory.Span[index];
                    // Map temp (-50 to 30) to a multiplier (e.g., 0.2 to 1.5)
                    var evapPower = Math.Clamp((tempAtTile + 50) / 80f, 0.2f, 1.5f);
                    cloudMoisture = MathF.Min(1.0f,
                        cloudMoisture + moistureRechargeRate * evapPower);
                }
                else
                {
                    // DISCHARGE: The 'previous' cell is where the wind came FROM.
                    // We subtract the windDir to look backward into the wind.
                    var prevX = WorldMath.WrapX(windX - windDir);
                    var prevElevation = elevationsF[y * _worldWidth + prevX];

                    var lift = elevation - prevElevation;
                    var rainDropped = 0f;

                    if (lift > 0)
                    {
                        // Squeeze moisture out
                        var desiredDrop = lift * cloudMoisture * rainDropFactor;
                        rainDropped = MathF.Min(cloudMoisture, desiredDrop);
                        cloudMoisture -= rainDropped;
                    }

                    if (isSecondPass)
                    {
                        // Apply shadow penalty, then add mountain rain
                        rainfall[windX, y] *= 0.5f + cloudMoisture * 0.5f;
                        rainfall[windX, y] += rainDropped;
                    }

                    // SHADOW decay
                    cloudMoisture *= 0.99f;
                }
            }
        }
    }

    /// <summary>
    ///     Fills local minima (pits) in the elevation data so that water can always flow to the ocean.
    ///     Implements a simplified Planchon-Darboux algorithm.
    /// </summary>
    private void FillDepressions()
    {
        var elevationsF = _elevation.Memory.Span;
        var filledElevations = new float[_worldWidth, _worldHeight];

        // Step 1: Initialize the water levels
        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
            {
                var elevation = elevationsF[y * _worldWidth + x];
                // If it's an ocean cell or on the top/bottom edge of the map, it's an outlet.
                if (elevation <= SeaLevelElevation || y == 0 || y == _worldHeight - 1)
                    filledElevations[x, y] = elevation;
                else
                    // Flood all inland cells to maximum height initially
                    filledElevations[x, y] = float.MaxValue;
            }

        // Step 2: Iteratively carve paths to the ocean
        var changed = true;
        while (changed)
        {
            changed = false;

            for (var y = 0; y < _worldHeight; y++)
                for (var x = 0; x < _worldWidth; x++)
                {
                    var currentElevation = elevationsF[y * _worldWidth + x];
                    if (Math.Abs(filledElevations[x, y] - currentElevation) < 0.1)
                        continue; // Already at minimum

                    var minNeighborWaterLevel = float.MaxValue;

                    // Check all 8 neighbors
                    for (var dy = -1; dy <= 1; dy++)
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;

                            var nx = WorldMath.WrapX(x + dx);
                            var ny = y + dy;

                            if (ny < 0 || ny >= _worldHeight) continue;

                            if (filledElevations[nx, ny] < minNeighborWaterLevel)
                                minNeighborWaterLevel = filledElevations[nx, ny];
                        }

                    // The new water level is the highest of either the actual terrain height, 
                    // or the lowest neighbor + a tiny slope to guarantee water flows across flats.
                    var newWaterLevel = MathF.Max(currentElevation, minNeighborWaterLevel + 0.001f);

                    if (newWaterLevel >= filledElevations[x, y]) continue;
                    filledElevations[x, y] = newWaterLevel;
                    changed = true;
                }
        }

        // Step 3: Apply the filled elevations back to the map
        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
                elevationsF[y * _worldWidth + x] = filledElevations[x, y];
    }

    // TODO: See whether we can reuse this flow field for other functions.

    /// <summary>
    ///     Calculates a map of hypothetical water flow directions for each cell.
    /// </summary>
    /// <returns>A grid of flow vectors per cell.</returns>
    private (int, int)[,] CalculateFlowDirections()
    {
        var elevationsF = _elevation.Memory.Span;
        var noiseMap = _noiseMap.Memory.Span;
        var flowDirections = new (int, int)[_worldWidth, _worldHeight];

        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
            {
                var currentElevation = elevationsF[y * _worldWidth + x];
                var steepestDrop = 0f;
                var bestDirection = (0, 0);

                // Check 4 neighbors
                (int, int)[] directions = [(0, -1), (1, 0), (0, 1), (-1, 0)];
                foreach (var (dx, dy) in directions)
                {
                    if (dx == 0 && dy == 0) continue;

                    var nx = WorldMath.WrapX(x + dx);
                    var ny = y + dy;

                    if (ny < 0 || ny >= _worldHeight) continue;

                    var neighborElevation = elevationsF[ny * _worldWidth + nx];
                    var drop = currentElevation - neighborElevation * noiseMap[ny * _worldWidth + nx];

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
    /// <param name="flowDirections">Direction of flow for each cell.</param>
    /// <param name="rainfall">Mapping of rainfall amount per cell.</param>
    /// <returns>Map of accumulated flow for each cell.</returns>
    private float[,] AccumulateFlow(
        in (int, int)[,] flowDirections,
        in float[,] rainfall)
    {
        var flowAccumulation = new float[_worldWidth, _worldHeight];
        var inDegree = new int[_worldWidth, _worldHeight];

        // Step 1: Initialize rainfall and calculate In-Degrees
        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
            {
                flowAccumulation[x, y] = rainfall[x, y];

                var (dx, dy) = flowDirections[x, y];
                if (dx == 0 && dy == 0) continue;

                var nx = WorldMath.WrapX(x + dx);
                var ny = y + dy;

                if (ny >= 0 && ny < _worldHeight)
                    // This neighbor is receiving flow from (x, y)
                    inDegree[nx, ny]++;
            }

        // Step 2: Queue all cells that have NO incoming water (Sources / Ridge lines)
        var queue = new Queue<(int, int)>();
        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
                if (inDegree[x, y] == 0)
                    queue.Enqueue((x, y));

        // Step 3: Process the queue
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            var (dx, dy) = flowDirections[x, y];

            if (dx == 0 && dy == 0) continue;

            var nx = WorldMath.WrapX(x + dx);
            var ny = y + dy;

            if (ny < 0 || ny >= _worldHeight) continue;

            // --- EVAPORATION LOGIC ---
            // If the local area is dry (low rainfall), evaporation is high.
            // Base evaporation rate (e.g., 0.02 means up to 2% volume lost per tile in dry areas)
            const float maxEvapRate = 0.05f;
            var aridity = 1.0f - rainfall[x, y];
            var evaporationLoss = aridity * maxEvapRate;

            // Calculate how much water actually makes it to the next tile
            var waterToPass = flowAccumulation[x, y] * (1.0f - evaporationLoss);

            // Pass the remaining water downstream
            flowAccumulation[nx, ny] += waterToPass;

            // Mark that one upstream dependency is resolved
            inDegree[nx, ny]--;

            // If all upstream dependencies are resolved, this cell is ready to flow
            if (inDegree[nx, ny] == 0) queue.Enqueue((nx, ny));
        }

        return flowAccumulation;
    }

    // TODO: Add to generation steps and visualisation.
    // TODO: Flatten result array and probably replace river map completely.

    /// <summary>
    ///     Calculates the Strahler Stream Order for the entire river network.
    /// </summary>
    /// <returns>
    ///     An integer matrix with the following valuation:
    ///     - Order 1-2: small creeks
    ///     - Order 3-4: small rivers
    ///     - Order  5+: major rivers
    /// </returns>
    private void CalculateStrahlerOrder((int, int)[,] flowDirections)
    {
        var strahlerRiver = _strahlerRiver.Memory.Span;
        var inDegree = new int[_worldWidth, _worldHeight];

        // Track the highest order seen so far for a cell, 
        // and whether we've seen it more than once.
        var maxIncomingOrder = new int[_worldWidth, _worldHeight];
        var countOfMaxOrder = new int[_worldWidth, _worldHeight];

        // 1. Calculate in-degrees (same as AccumulateFlow)
        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
            {
                var (dx, dy) = flowDirections[x, y];
                if (dx == 0 && dy == 0) continue;
                var nx = WorldMath.WrapX(x + dx);
                var ny = y + dy;
                if (ny >= 0 && ny < _worldHeight) inDegree[nx, ny]++;
            }

        // 2. Queue the sources (Order 1)
        var queue = new Queue<(int, int)>();
        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
                if (inDegree[x, y] == 0)
                {
                    strahlerRiver[y * _worldWidth + x] = 1;
                    queue.Enqueue((x, y));
                }

        // 3. Process topologically
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            var currentOrder = strahlerRiver[y * _worldWidth + x];

            var (dx, dy) = flowDirections[x, y];
            if (dx == 0 && dy == 0) continue;

            var nx = WorldMath.WrapX(x + dx);
            var ny = y + dy;
            if (ny < 0 || ny >= _worldHeight) continue;

            // Update the neighbor's knowledge of its incoming tributaries
            if (currentOrder > maxIncomingOrder[nx, ny])
            {
                maxIncomingOrder[nx, ny] = currentOrder;
                countOfMaxOrder[nx, ny] = 1;
            }
            else if (currentOrder == maxIncomingOrder[nx, ny])
            {
                countOfMaxOrder[nx, ny]++;
            }

            inDegree[nx, ny]--;
            if (inDegree[nx, ny] > 0) continue;
            // RESOLVE ORDER: If two or more tributaries of the same MAX order meet, level up.
            // Otherwise, inherit the max incoming order.
            strahlerRiver[ny * _worldWidth + nx] = countOfMaxOrder[nx, ny] >= 2
                ? maxIncomingOrder[nx, ny] + 1
                : maxIncomingOrder[nx, ny];

            queue.Enqueue((nx, ny));
        }
    }

    /// <summary>
    ///     Create a boolean river map of the world where true -> belongs to a river, false otherwise.
    /// </summary>
    /// <param name="flowDirections">Flow direction for each cell.</param>
    /// <param name="flowAccumulation">Accumulated flow for each cell.</param>
    private void CarveRivers((int, int)[,] flowDirections, float[,] flowAccumulation)
    {
        var elevationsF = _elevation.Memory.Span;
        var riverMap = _riverMap.Memory.Span;
        var strahlerRiver = _strahlerRiver.Memory.Span;
        riverMap.Clear();

        // 1. Identify "Major" vs "Minor" for normalization
        var maxOrder = 0;
        for (var i = 0; i < _worldWidth * _worldHeight; i++)
            maxOrder = Math.Max(maxOrder, strahlerRiver[i]);

        // 2. Visual and Physical Thresholds
        const int minOrderToVisualise = 2;
        const int minOrderToCarve = 3;

        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
            {
                var order = strahlerRiver[y * _worldWidth + x];
                if (order < minOrderToCarve || elevationsF[y * _worldWidth + x] <= SeaLevelElevation)
                    continue;

                // We only trace from the 'tips' of the network to avoid redundant carving
                // (If inDegree is 0, it's a headwater/source)
                // Note: You'll need to pass inDegree or calculate it, or just keep the trace logic.
                // Let's stick to your trace logic but check the 'riverMap' to prevent double-carving.

                var cx = x;
                var cy = y;
                var maxSteps = _worldWidth + _worldHeight;

                while (maxSteps-- > 0)
                {
                    var index = cy * _worldWidth + cx;
                    var currentOrder = strahlerRiver[index];

                    // --- THE STRAHLER UPGRADE ---

                    // A. Visual Overlay: Only show the big boys
                    if (currentOrder >= minOrderToVisualise)
                        riverMap[index] = true;

                    // B. Physical Carving: Depth scales with ACTUAL WATER VOLUME (Flow Accumulation)
                    // Assuming base rainfall is roughly 1.0 per cell, a flow accumulation of 100 means 100 cells drained here.
                    var waterVolume = flowAccumulation[cx, cy];
                    var erosionPower = MathF.Pow(waterVolume, 0.1f) * 0.01f; // Tweak exponents to taste

                    var cellElevation = elevationsF[index];
                    var targetDepth = MathF.Min(RiverMaxCarveDepth, erosionPower * RiverCarveScale);

                    // Only carve if we are deeper than the current elevation
                    var newElevation = MathF.Max(RiverCarveMinElevation, cellElevation - targetDepth);
                    elevationsF[index] = MathF.Min(cellElevation, newElevation);

                    // Stop if we hit the sea
                    if (elevationsF[index] <= SeaLevelElevation) break;

                    var (dx, dy) = flowDirections[cx, cy];
                    if (dx == 0 && dy == 0) break;

                    cx = WorldMath.WrapX(cx + dx);
                    cy += dy;

                    if (cy < 0 || cy >= _worldHeight) break;
                }
            }
    }

    /// <summary>
    ///     Deposits sediments carried along by the rivers.
    /// </summary>
    /// <param name="flowAccumulation">Map of flow accumulation per cell.</param>
    private void DepositSediment(
        float[,] flowAccumulation)
    {
        var elevationsF = _elevation.Memory.Span;

        // Only rivers carrying a significant amount of water deposit noticeable sediment
        const float sedimentThreshold = 50f;

        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
            {
                var waterVolume = flowAccumulation[x, y];
                if (waterVolume < sedimentThreshold) continue;

                var currentElevation = elevationsF[y * _worldWidth + x];

                // Deposit sediment in shallow waters (forming river deltas)
                // or in extremely flat lowlands (floodplains)
                if (!(currentElevation <= SeaLevelElevation) ||
                    !(currentElevation > SeaLevelElevation - 3)) continue;
                // The more water, the more sediment. 
                var sedimentAmount = waterVolume * 0.01f;

                // Build up the land, potentially creating new landmasses (deltas) right at the coast
                elevationsF[y * _worldWidth + x] =
                    Math.Min(SeaLevelElevation + 1f, currentElevation + sedimentAmount);
            }
    }

    /// <summary>
    ///     Combines rainfall, river proximity, and elevation to create a final humidity map.
    /// </summary>
    private void GenerateMoistureMap(float[,] rainfall)
    {
        var elevationsF = _elevation.Memory.Span;
        var riverMap = _riverMap.Memory.Span;
        var strahlerRiver = _strahlerRiver.Memory.Span;
        var humidity = _humidity.Memory.Span;

        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
            {
                var index = y * _worldWidth + x;
                var elevation = elevationsF[index];
                var strahler = strahlerRiver[y * _worldWidth + x];
                //     - Order 1-2: small creeks
                //     - Order 3-4: small rivers
                //     - Order  5+: major rivers
                var neighborhood = strahler switch
                {
                    <= 2 => new[] { (0, 0) },
                    <= 4 => new[] { (0, 1), (0, -1), (1, 0), (-1, 0) },
                    _ => new[]
                    {
                    (0, 1), (0, -1), (1, 0), (-1, 0),
                    (-1, 1), (1, -1), (1, 1), (-1, -1),
                    (0, 2), (0, -2), (2, 0), (-2, 0)
                }
                };

                // 1. Start with the base rainfall
                var baseMoisture = rainfall[x, y];

                // 2. Add a bonus for being near a river (Floodplain effect)
                var riverBonus = 0f;
                if (riverMap[index])
                    riverBonus = 0.3f; // Significant boost for the river cell itself
                else
                    // Simple 1-pixel neighbor check for "lush banks"
                    foreach (var (dx, dy) in neighborhood)
                    {
                        var nx = WorldMath.WrapX(x + dx);
                        var ny = y + dy;
                        if (ny < 0 || ny >= _worldHeight || !riverMap[ny * _worldWidth + nx]) continue;
                        riverBonus = 0.15f;
                        break;
                    }

                // 3. Elevation Penalty: Higher = Drier (simplified drainage)
                // We assume anything above SeaLevelElevation starts losing moisture retention
                var elevationFactor = MathF.Max(0.2f, 1.0f - elevation / MaxElevation);

                // 4. Combine and Clamp
                humidity[index] = Math.Clamp((baseMoisture + riverBonus) * elevationFactor, 0f, 1f);
            }

        // 5. Optional: Blur the moisture map slightly to create smoother biome transitions
        // return moisture;  //SmoothMoisture(moisture);
    }

    #endregion

    #region Mountains

    /// <summary>
    ///     Applies surface features to mountains, snow, glaciers, rivers and lava.
    /// </summary>
    private void ApplyMountainDetails()
    {
        var elevations = _elevation.Memory.Span;
        var surfaceMap = _surfaceFeatures.Memory.Span;
        var riverMap = _riverMap.Memory.Span;
        var hotspotMap = _hotspotMap.Memory.Span;
        var temperatures = _temperature.Memory.Span;

        for (var i = 0; i < _worldWidth * _worldHeight; i++)
        {
            var elevation = elevations[i];
            var temperature = temperatures[i];

            surfaceMap[i] = SurfaceFeature.None;
            switch (temperature)
            {
                // 1. Check for Glaciers (Requires freezing temps and moisture, no rivers)
                // Assuming temperature < 0.1f is deep freeze
                case < 0.1f when !riverMap[i] && elevation > SeaLevelElevation:
                    surfaceMap[i] = SurfaceFeature.Glacier;
                    continue;
                // 2. Check for Snow (Slightly warmer than glaciers, or high mountains)
                case < 0.25f when elevation > SeaLevelElevation:
                    surfaceMap[i] = SurfaceFeature.Snow;
                    continue;
            }

            // 3. Check for Mountains (Purely geographical)
            if (elevation >= HighMountainThreshold)
            {
                surfaceMap[i] = SurfaceFeature.Mountain;
                continue;
            }

            if (riverMap[i])
            {
                surfaceMap[i] = SurfaceFeature.River;
                continue;
            }

            if (hotspotMap[i] > LavaHotspotThreshold && elevation >= LandElevationThreshold)
                surfaceMap[i] = SurfaceFeature.Lava;
        }

        // Add volcanic details around hotspots
        var hotspotCenters = FindHotspotCenters();
        foreach (var (centerX, centerY, strength) in hotspotCenters)
            AddVolcanicDetails(centerX, centerY, strength);

        var plateTypes = _plateTypes.Memory.Span;
        var plateIndex = _plateIndex.Memory.Span;
        var voronoiIndex = _voronoiCellIndex.Memory.Span;

        // Plate boundary mountain ridges: continental collisions
        var neighbors = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
            {
                var currentPlate = plateIndex[voronoiIndex[y * _worldWidth + x]];
                if (currentPlate < 0 || currentPlate >= plateTypes.Length) continue;

                foreach (var (dx, dy) in neighbors)
                {
                    var nx = WorldMath.WrapX(x + dx);
                    var ny = y + dy;

                    if (ny < 0 || ny >= _worldHeight) continue;

                    var neighborPlate = plateIndex[voronoiIndex[ny * _worldWidth + nx]];
                    if (neighborPlate == currentPlate) continue;

                    if (neighborPlate < 0 || neighborPlate >= plateTypes.Length) continue;

                    // Skip undersea tiles.
                    if (elevations[y * _worldWidth + x] <= SeaLevelElevation) continue;
                    // Skip tiles with features already defined on them.
                    if (surfaceMap[y * _worldWidth + x] != SurfaceFeature.None) continue;

                    surfaceMap[y * _worldWidth + x] = SurfaceFeature.Mountain;
                }
            }
    }

    // TODO: Didn't we calculate hotspot centers already?

    /// <summary>
    ///     Finds center points of strong hot spots.
    /// </summary>
    /// <returns>List of hotspot centers</returns>
    private List<(int x, int y, float strength)> FindHotspotCenters()
    {
        var hotspotMap = _hotspotMap.Memory.Span;
        var centers = new List<(int, int, float)>();
        var visited = new bool[_worldWidth, _worldHeight];

        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
            {
                if (visited[x, y] || hotspotMap[y * _worldWidth + x] < HotspotMinStrength) continue;

                // Find local maximum
                var maxStrength = hotspotMap[y * _worldWidth + x];
                var maxX = x;
                var maxY = y;

                // Search in a small radius for the actual peak
                for (var dy = -3; dy <= 3; dy++)
                    for (var dx = -3; dx <= 3; dx++)
                    {
                        var nx = WorldMath.WrapX(x + dx);
                        var ny = y + dy;
                        if (ny < 0 || ny >= _worldHeight) continue;
                        if (hotspotMap[ny * _worldWidth + nx] <= maxStrength) continue;

                        maxStrength = hotspotMap[ny * _worldWidth + nx];
                        maxX = nx;
                        maxY = ny;
                    }

                // Mark area around peak as visited
                for (var dy = -5; dy <= 5; dy++)
                    for (var dx = -5; dx <= 5; dx++)
                    {
                        var nx = WorldMath.WrapX(maxX + dx);
                        var ny = maxY + dy;
                        if (ny >= 0 && ny < _worldHeight)
                            visited[nx, ny] = true;
                    }

                centers.Add((maxX, maxY, maxStrength));
            }

        return centers;
    }

    private void AddVolcanicDetails(int centerX, int centerY, float strength)
    {
        var elevationsF = _elevation.Memory.Span;
        var surfaceMap = _surfaceFeatures.Memory.Span;
        var radius = Math.Max(3, (int)(strength * 10)); // Scale radius with strength, minimum 3

        // Determine volcano type based on strength and characteristics
        var volcanoType = strength switch
        {
            > 0.9f => SurfaceFeature.Stratovolcano,
            > 0.6f => SurfaceFeature.Shield,
            _ => SurfaceFeature.Cinder
        };

        // Add crater at the peak for stratovolcanoes and large shields
        if (centerX >= 0 && centerX < _worldWidth && centerY >= 0 && centerY < _worldHeight)
        {
            var elevation = elevationsF[centerY * _worldWidth + centerX];
            surfaceMap[centerY * _worldWidth + centerX] = elevation switch
            {
                >= CraterElevationThreshold when volcanoType == SurfaceFeature.Stratovolcano ||
                                                 (volcanoType == SurfaceFeature.Shield &&
                                                  strength > 0.8f) => SurfaceFeature.Crater,
                >= CinderElevationThreshold when volcanoType == SurfaceFeature.Cinder =>
                    SurfaceFeature.Cinder,
                _ => surfaceMap[centerY * _worldWidth + centerX]
            };
        }

        // Add volcanic features based on volcano type
        for (var y = Math.Max(0, centerY - radius);
             y < Math.Min(_worldHeight, centerY + radius);
             y++)
            for (var x = centerX - radius; x < centerX + radius; x++)
            {
                var wrappedX = WorldMath.WrapX(x);
                var distance = WorldMath.GetCylindricalDistance(wrappedX, y, centerX, centerY);

                if (distance > radius) continue;

                var normalizedDist = distance / radius;
                var elevation = elevationsF[y * _worldWidth + wrappedX];

                // Skip if already has strong features
                var existing = surfaceMap[y * _worldWidth + wrappedX];
                if (existing is SurfaceFeature.River or SurfaceFeature.Glacier or SurfaceFeature.Lava)
                    continue;

                surfaceMap[y * _worldWidth + wrappedX] = volcanoType switch
                {
                    SurfaceFeature.Stratovolcano => normalizedDist switch
                    {
                        // Stratovolcanoes: steep, explosive, with ash and lava
                        < 0.4f when elevation >= CalderaElevationThreshold => SurfaceFeature
                            .Stratovolcano,
                        > 0.3f and < 0.8f when elevation >= 5 => SurfaceFeature.Ash,
                        _ => surfaceMap[y * _worldWidth + wrappedX]
                    },
                    SurfaceFeature.Shield => normalizedDist switch
                    {
                        // Shield volcanoes: broad, gentle slopes, mostly lava
                        < 0.6f when elevation >= ShieldVolcanoThreshold => SurfaceFeature.Shield,
                        > 0.5f when elevation >= 4 => SurfaceFeature.Lava,
                        _ => surfaceMap[y * _worldWidth + wrappedX]
                    },
                    _ => normalizedDist switch
                    {
                        // Cinder cones: small, steep, cinder and ash
                        < 0.5f when elevation >= 4 => SurfaceFeature.Cinder,
                        > 0.4f when elevation >= 3 => SurfaceFeature.Ash,
                        _ => surfaceMap[y * _worldWidth + wrappedX]
                    }
                };

                // Add caldera for large stratovolcanoes at the center
                if (normalizedDist < 0.2f && volcanoType == SurfaceFeature.Stratovolcano &&
                    strength > 0.9f &&
                    elevation >= 7) surfaceMap[y * _worldWidth + wrappedX] = SurfaceFeature.Caldera;
            }
    }

    #endregion

    #region Coastal Features

    /// <summary>
    ///     Detect coastal features depending on their surrounding: beaches, cliffs and fjords.
    /// </summary>
    private void ApplyCoastalFeatures()
    {
        var elevationsF = _elevation.Memory.Span;
        var surfaceMap = _surfaceFeatures.Memory.Span;
        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
            {
                // Skip existing assigned strong surface features: river, lava, glacier
                var existing = surfaceMap[y * _worldWidth + x];
                if (existing is SurfaceFeature.River or SurfaceFeature.Lava or SurfaceFeature.Glacier)
                    continue;

                var elevation = elevationsF[y * _worldWidth + x];
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
                            var nx = WorldMath.WrapX(x + dx);
                            var ny = y + dy;
                            if (ny < 0 || ny >= _worldHeight) continue;

                            var neighborElevation = elevationsF[ny * _worldWidth + nx];
                            if (neighborElevation <= SeaLevelElevation)
                                adjacentWater++;
                            else
                                maxAdjElevation = Math.Max(maxAdjElevation, neighborElevation);
                        }

                    if (adjacentWater > 0)
                    {
                        var slope =
                            elevation - SeaLevelElevation; // How high is this coast above the water?

                        switch (slope)
                        {
                            // Steep drop into the sea
                            case >= 3.0f:
                                surfaceMap[y * _worldWidth + x] = SurfaceFeature.Cliff;
                                break;
                            // Gentle transition into the sea
                            case <= 1.0f:
                                {
                                    // Don't overwrite existing mountain features from previous steps
                                    if (surfaceMap[y * _worldWidth + x] == SurfaceFeature.None)
                                        surfaceMap[y * _worldWidth + x] = SurfaceFeature.Beach;

                                    break;
                                }
                        }
                        // If slope is intermediate, leave it as regular land (None/Grass/Forest)
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
                            var nx = WorldMath.WrapX(x + dx);
                            var ny = y + dy;
                            if (ny < 0 || ny >= _worldHeight) continue;
                            var neighborElevation = elevationsF[ny * _worldWidth + nx];
                            if (neighborElevation < LandElevationThreshold) continue;
                            adjacentLand++;
                            if (neighborElevation >= SnowThreshold - 1)
                                adjacentHighMountain++;
                        }

                    if (adjacentLand >= 3 && adjacentHighMountain >= 1)
                        surfaceMap[y * _worldWidth + x] = SurfaceFeature.Fjord;
                }
            }
    }

    #endregion

    #region To ECS Components

    private WorldElevationChunk[] ToElevationChunks()
    {
        var worldSpan = _elevation.Memory.Span;

        for (var i = 0; i < WorldMath.WorldWidth * WorldMath.WorldHeight; i++)
            _maxElevation = MathF.Max(_maxElevation, worldSpan[i]);

        const int chunkSize = WorldMath.ChunkSize;
        const int worldWidth = WorldMath.WorldWidth;
        const int worldHeight = WorldMath.WorldHeight;
        const int chunksAcross = worldWidth / chunkSize;

        var chunks = new WorldElevationChunk[chunksAcross * (worldHeight / chunkSize)];

        for (var cy = 0; cy < worldHeight; cy += chunkSize)
            for (var cx = 0; cx < worldWidth; cx += chunkSize)
            {
                var chunkXIndex = cx / chunkSize;
                var chunkYIndex = cy / chunkSize;
                var chunkIdx = chunkYIndex * chunksAcross + chunkXIndex;
                var chunk = new int[chunkSize * chunkSize];

                // Copy rows from world-layout to contiguous chunk-layout
                for (var ly = 0; ly < chunkSize; ly++)
                {
                    var sourceStart = (cy + ly) * worldWidth + cx;
                    var sourceRow = worldSpan.Slice(sourceStart, chunkSize);
                    var destRow = chunk.AsSpan().Slice(ly * chunkSize, chunkSize);
                    for (var i = 0; i < sourceRow.Length; i++)
                    {
                        var normalisedElevation = sourceRow[i] / _maxElevation * 9;
                        var elevationClamped = Math.Max(0, normalisedElevation);
                        destRow[i] = Convert.ToInt32(elevationClamped);
                    }
                }

                // The chunk now holds a 'view' of the master buffer, not a unique array
                chunks[chunkIdx] =
                    new WorldElevationChunk(chunkIdx, chunkXIndex, chunkYIndex, chunk);
            }

        return chunks;
    }

    private WorldSurfaceFeatureChunk[] ToSurfaceFeatureChunks()
    {
        var worldSpan = _surfaceFeatures.Memory.Span;
        const int chunkSize = WorldMath.ChunkSize;
        const int worldWidth = WorldMath.WorldWidth;
        const int worldHeight = WorldMath.WorldHeight;
        const int chunksAcross = worldWidth / chunkSize;

        var chunks = new WorldSurfaceFeatureChunk[chunksAcross * (worldHeight / chunkSize)];

        for (var cy = 0; cy < worldHeight; cy += chunkSize)
            for (var cx = 0; cx < worldWidth; cx += chunkSize)
            {
                var chunkXIndex = cx / chunkSize;
                var chunkYIndex = cy / chunkSize;
                var chunkIdx = chunkYIndex * chunksAcross + chunkXIndex;
                var chunk = new SurfaceFeature[chunkSize * chunkSize];

                // Copy rows from world-layout to contiguous chunk-layout
                for (var ly = 0; ly < chunkSize; ly++)
                {
                    var sourceStart = (cy + ly) * worldWidth + cx;
                    var sourceRow = worldSpan.Slice(sourceStart, chunkSize);
                    var destRow = chunk.AsSpan().Slice(ly * chunkSize, chunkSize);
                    sourceRow.CopyTo(destRow);
                }

                // The chunk now holds a 'view' of the master buffer, not a unique array
                chunks[chunkIdx] =
                    new WorldSurfaceFeatureChunk(chunkIdx, chunkXIndex, chunkYIndex, chunk);
            }

        return chunks;
    }

    private WorldTemperatureChunk[] ToTemperatureChunks()
    {
        var worldSpan = _temperature.Memory.Span;
        const int chunkSize = WorldMath.ChunkSize;
        const int worldWidth = WorldMath.WorldWidth;
        const int worldHeight = WorldMath.WorldHeight;
        const int chunksAcross = worldWidth / chunkSize;

        var chunks = new WorldTemperatureChunk[chunksAcross * (worldHeight / chunkSize)];

        for (var cy = 0; cy < worldHeight; cy += chunkSize)
            for (var cx = 0; cx < worldWidth; cx += chunkSize)
            {
                var chunkXIndex = cx / chunkSize;
                var chunkYIndex = cy / chunkSize;
                var chunkIdx = chunkYIndex * chunksAcross + chunkXIndex;
                var chunk = new float[chunkSize * chunkSize];

                // Copy rows from world-layout to contiguous chunk-layout
                for (var ly = 0; ly < chunkSize; ly++)
                {
                    var sourceStart = (cy + ly) * worldWidth + cx;
                    var sourceRow = worldSpan.Slice(sourceStart, chunkSize);
                    var destRow = chunk.AsSpan().Slice(ly * chunkSize, chunkSize);
                    sourceRow.CopyTo(destRow);
                }

                // The chunk now holds a 'view' of the master buffer, not a unique array
                chunks[chunkIdx] =
                    new WorldTemperatureChunk(chunkIdx, chunkXIndex, chunkYIndex, chunk);
            }

        return chunks;
    }

    private WorldTemperatureAmplitudeChunk[] ToTemperatureAmplitudeChunks()
    {
        var worldSpan = _temperatureAmplitude.Memory.Span;
        const int chunkSize = WorldMath.ChunkSize;
        const int worldWidth = WorldMath.WorldWidth;
        const int worldHeight = WorldMath.WorldHeight;
        const int chunksAcross = worldWidth / chunkSize;

        var chunks = new WorldTemperatureAmplitudeChunk[chunksAcross * (worldHeight / chunkSize)];

        for (var cy = 0; cy < worldHeight; cy += chunkSize)
            for (var cx = 0; cx < worldWidth; cx += chunkSize)
            {
                var chunkXIndex = cx / chunkSize;
                var chunkYIndex = cy / chunkSize;
                var chunkIdx = chunkYIndex * chunksAcross + chunkXIndex;
                var chunk = new float[chunkSize * chunkSize];

                // Copy rows from world-layout to contiguous chunk-layout
                for (var ly = 0; ly < chunkSize; ly++)
                {
                    var sourceStart = (cy + ly) * worldWidth + cx;
                    var sourceRow = worldSpan.Slice(sourceStart, chunkSize);
                    var destRow = chunk.AsSpan().Slice(ly * chunkSize, chunkSize);
                    sourceRow.CopyTo(destRow);
                }

                // The chunk now holds a 'view' of the master buffer, not a unique array
                chunks[chunkIdx] =
                    new WorldTemperatureAmplitudeChunk(chunkIdx, chunkXIndex, chunkYIndex, chunk);
            }

        return chunks;
    }

    private WorldHumidityChunk[] ToHumidityChunks()
    {
        var worldSpan = _humidity.Memory.Span;
        const int chunkSize = WorldMath.ChunkSize;
        const int worldWidth = WorldMath.WorldWidth;
        const int worldHeight = WorldMath.WorldHeight;
        const int chunksAcross = worldWidth / chunkSize;

        var chunks = new WorldHumidityChunk[chunksAcross * (worldHeight / chunkSize)];

        for (var cy = 0; cy < worldHeight; cy += chunkSize)
            for (var cx = 0; cx < worldWidth; cx += chunkSize)
            {
                var chunkXIndex = cx / chunkSize;
                var chunkYIndex = cy / chunkSize;
                var chunkIdx = chunkYIndex * chunksAcross + chunkXIndex;
                var chunk = new float[chunkSize * chunkSize];

                // Copy rows from world-layout to contiguous chunk-layout
                for (var ly = 0; ly < chunkSize; ly++)
                {
                    var sourceStart = (cy + ly) * worldWidth + cx;
                    var sourceRow = worldSpan.Slice(sourceStart, chunkSize);
                    var destRow = chunk.AsSpan().Slice(ly * chunkSize, chunkSize);
                    sourceRow.CopyTo(destRow);
                }

                // The chunk now holds a 'view' of the master buffer, not a unique array
                chunks[chunkIdx] =
                    new WorldHumidityChunk(chunkIdx, chunkXIndex, chunkYIndex, chunk);
            }

        return chunks;
    }

    private WorldBiomeChunk[] ToBiomeChunks()
    {
        var worldSpan = _biomes.Memory.Span;
        const int chunkSize = WorldMath.ChunkSize;
        const int worldWidth = WorldMath.WorldWidth;
        const int worldHeight = WorldMath.WorldHeight;

        var chunks = new WorldBiomeChunk[WorldMath.ChunksAcross * (worldHeight / chunkSize)];

        for (var cy = 0; cy < worldHeight; cy += chunkSize)
            for (var cx = 0; cx < worldWidth; cx += chunkSize)
            {
                var chunkXIndex = cx / chunkSize;
                var chunkYIndex = cy / chunkSize;
                var chunkIdx = chunkYIndex * WorldMath.ChunksAcross + chunkXIndex;
                var chunk = new Biome[chunkSize * chunkSize];

                // Copy rows from world-layout to contiguous chunk-layout
                for (var ly = 0; ly < chunkSize; ly++)
                {
                    var sourceStart = (cy + ly) * worldWidth + cx;
                    var sourceRow = worldSpan.Slice(sourceStart, chunkSize);
                    var destRow = chunk.AsSpan().Slice(ly * chunkSize, chunkSize);
                    sourceRow.CopyTo(destRow);
                }

                // The chunk now holds a 'view' of the master buffer, not a unique array
                chunks[chunkIdx] =
                    new WorldBiomeChunk(chunkIdx, chunkXIndex, chunkYIndex, chunk);
            }

        return chunks;
    }

    private WorldRiverChunk[] ToRiverChunks()
    {
        var worldSpan = _riverMap.Memory.Span;
        const int chunkSize = WorldMath.ChunkSize;
        const int worldWidth = WorldMath.WorldWidth;
        const int worldHeight = WorldMath.WorldHeight;
        const int chunksAcross = worldWidth / chunkSize;

        var chunks = new WorldRiverChunk[chunksAcross * (worldHeight / chunkSize)];

        for (var cy = 0; cy < worldHeight; cy += chunkSize)
            for (var cx = 0; cx < worldWidth; cx += chunkSize)
            {
                var chunkXIndex = cx / chunkSize;
                var chunkYIndex = cy / chunkSize;
                var chunkIdx = chunkYIndex * chunksAcross + chunkXIndex;
                var chunk = new bool[chunkSize * chunkSize];

                // Copy rows from world-layout to contiguous chunk-layout
                for (var ly = 0; ly < chunkSize; ly++)
                {
                    var sourceStart = (cy + ly) * worldWidth + cx;
                    var sourceRow = worldSpan.Slice(sourceStart, chunkSize);
                    var destRow = chunk.AsSpan().Slice(ly * chunkSize, chunkSize);
                    sourceRow.CopyTo(destRow);
                }

                // The chunk now holds a 'view' of the master buffer, not a unique array
                chunks[chunkIdx] =
                    new WorldRiverChunk(chunkIdx, chunkXIndex, chunkYIndex, chunk);
            }

        return chunks;
    }

    #endregion
}