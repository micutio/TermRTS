using System.Buffers;
using System.Numerics;
using TermRTS.Examples.Greenery.Ecs.Component;

namespace TermRTS.Examples.Greenery.WorldGen;

// Refer to link below for a nice layered noise map implementation:
// https://github.com/SebLague/Procedural-Landmass-Generation/blob/master/Proc%20Gen%20E03/Assets/Scripts/Noise.cs

// TODO: Combine surface feature and biome into one enum?

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
    HighSeas,
    Ocean,
    Shelf,
    Shallows,
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

public sealed class WorldGenerationResult(WorldPackedChunk[] packedData)
{
    public WorldPackedChunk[] PackedData { get; } = packedData;
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
    #region Fields

    private readonly Random _rng;
    private readonly int _seed;
    private readonly int _worldWidth;
    private readonly int _worldHeight;
    private readonly int _plateCount;

    private readonly ElevationParameters _elevationCfg;
    private readonly CoastalParameters _coastalCfg;
    private readonly VolcanicParameters _volcanicCfg;
    private readonly ErosionParameters _erosionCfg;
    private readonly ClimateParameters _climateCfg;
    private readonly RiverParameters _riverCfg;

    private float _maxElevation = float.MinValue;
    private float _minTectonicDelta = float.MaxValue;
    private float _maxTectonicDelta = float.MinValue;
    private float _minHotspotHeight = float.MaxValue;
    private float _maxHotspotHeight = float.MinValue;

    // TODO: Clean up this mess of buffers.
    private readonly WorldBuffer<float> _elevation;
    private readonly WorldBuffer<int> _landWaterMap;
    private readonly WorldBuffer<(int, int)> _voronoiCells;
    private readonly WorldBuffer<bool> _voronoiCellTypes;
    private readonly WorldBuffer<int> _voronoiCellIndex;
    private readonly WorldBuffer<(int, int)> _plateCells;
    private readonly WorldBuffer<bool> _plateTypes;
    private readonly WorldBuffer<int> _plateIndex;
    private readonly WorldBuffer<Vector2> _plateMotions;
    private readonly WorldBuffer<(int, int)> _flowDirections;
    private readonly WorldBuffer<(int, int)> _windDirections;
    private readonly WorldBuffer<byte> _windSpeeds;
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

    #endregion

    #region Constructor

    public CylinderWorld(
        int worldWidth,
        int worldHeight,
        float landRatio,
        int seed,
        int voronoiCellCount,
        int plateCount,
        ElevationParameters elevationCfg,
        CoastalParameters coastalCfg,
        VolcanicParameters volcanicCfg,
        ErosionParameters erosionCfg,
        ClimateParameters climateCfg,
        RiverParameters riverCfg)
    {
        // init readonly fields
        _rng = new Random(seed);
        _seed = seed;
        _worldWidth = worldWidth;
        _worldHeight = worldHeight;
        LandRatio = landRatio;
        _plateCount = plateCount;

        _elevationCfg = elevationCfg;
        _coastalCfg = coastalCfg;
        _volcanicCfg = volcanicCfg;
        _erosionCfg = erosionCfg;
        _climateCfg = climateCfg;
        _riverCfg = riverCfg;

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
        _flowDirections = new WorldBuffer<(int, int)>(worldWidth * worldHeight);
        _windDirections = new WorldBuffer<(int, int)>(worldWidth * worldHeight);
        _windSpeeds = new WorldBuffer<byte>(worldWidth * worldHeight);
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
        // Stage 0: Preparation ~> Noise Map ///////////////////////////////////////////////////////
        GenerateNoiseMap();

        // STAGE 1: Voronoi Cells and Land/Water distribution //////////////////////////////////////
        // Associate each grid cell to one of the voronoi cells.
        // For each voronoi land cell, apply perlin or simplex noise to generate height.
        InitializeVoronoiCells();
        GenerateLandWaterDistribution();

        // Generate coastal slopes for each voronoi cell.
        // GenerateSlopedCoasts();

        // Stage 2: Plate Tectonics ///////////////////////////////////////////////////////////////
        InitializePlateTectonics();
        // Compute plate tectonics influence (mountains/trenches along plate boundaries).
        ComputePlateTectonicHeight();
        // Generate hotspots (mantle plumes creating volcanic islands/seamounts).
        GenerateHotspots();
        // Apply all elevation changes from tectonics, hotspots etc.
        ApplyTectonics();

        // Stage 3: Climate ////////////////////////////////////////////////////////////////////////
        // Generate wind field now that elevation and noise are available. Wind
        // is used by the rainfall/shadow simulation and must be generated
        // before biome assignment.
        CalculateWindField();

        // CalculateDistanceToWaterMap(); // TODO: This does not seem to be used.
        // Generate climate (temperature, humidity, biomes, seasonal effects)
        GenerateClimate();
        // Apply erosion to smooth terrain and create realistic features
        ApplyErosion();
        // Generate rivers based on rainfall and elevation (tunable via public properties)
        GenerateRivers();

        // TODO: Re-generate distance to water?
        // Re-generate climate, now that we have rivers:
        GenerateClimate();
        // Apply mountain details (ridges, snow, glacier, lava)
        ApplyMountainDetails();
        // Apply coastal features (beach, cliff, fjord)
        ApplyCoastalFeatures();

        // Wind field was calculated earlier (before biome generation).

        return new WorldGenerationResult(ToPackedChunks());
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
        _windDirections.Memory.Span.Clear();
        _windSpeeds.Memory.Span.Clear();
        _strahlerRiver.Memory.Span.Clear();
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
            landWater[i] = pTypes[i]
                ? _elevationCfg.LandElevationThreshold
                : _elevationCfg.LandElevationThreshold - 1;
    }

    /// <summary>
    ///     Generates the type for each plate:
    ///     true == continental, false == oceanic.
    /// </summary>
    private void GenerateVoronoiCellTypes()
    {
        var pTypes = _voronoiCellTypes.Memory.Span;

        var land = 0f;
        var water = 0f;
        for (var i = 0; i < VoronoiCellCount; i += 1)
        {
            pTypes[i] = _rng.NextDouble() < LandRatio;
            if (pTypes[i])
            {
                land++;
            }
            else
            {
                water++;
            }
        }

        var ratio = land / (land + water);
        Console.WriteLine($"Generate Voronoi Cell Types: {ratio * 100} % land");
    }

    /// <summary>
    ///     Assigns an elevation and voronoi cell to each single cell on the map.
    /// </summary>
    private void GenerateLandWaterDistribution()
    {
        var noiseMap = _noiseMap.Memory.Span;
        const int jiggle = 20;
        var vCells = _voronoiCells.Memory.Span;
        var vIdx = _voronoiCellIndex.Memory.Span;
        var elevations = _elevation.Memory.Span;
        var landWater = _landWaterMap.Memory.Span;

        var land = 0f;
        var water = 0f;

        for (var y = 0; y < _worldHeight; y += 1)
            for (var x = 0; x < _worldWidth; x += 1)
            {
                var idx = y * _worldWidth + x;
                var jiggleNoise = noiseMap[idx];
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

                vIdx[idx] = winnerCell;
                elevations[idx] = landWater[winnerCell] >= _elevationCfg.LandElevationThreshold
                    ? _elevationCfg.LandElevationThreshold + MathF.Pow(noiseMap[idx], 1) *
                    (_elevationCfg.MaxElevation - _elevationCfg.LandElevationThreshold - 1)
                    : noiseMap[idx] * (_elevationCfg.LandElevationThreshold - 1);

                if (elevations[idx] >= _elevationCfg.LandElevationThreshold)
                {
                    land++;
                }
                else
                {
                    water++;
                }
            }

        var ratio = land / (land + water);
        Console.WriteLine($"Generate Land Water Distribution: {ratio * 100} % land");
    }

    #endregion

    #region Tectonics

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
            var speed = (float)_rng.NextDouble() * 1.5f; // * 0.5 + 0.1);
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
            coastalSlopes[i] = _coastalCfg.MaxCoastalSlope;

        var q = new Queue<(int, int)>(_worldWidth * _worldHeight);
        for (var y = 1; y < _worldHeight - 1; y += 1)
            for (var x = 1; x < _worldWidth - 1; x += 1)
            {
                if (elevations[y * _worldWidth + x] >= _elevationCfg.LandElevationThreshold)
                    continue;

                // Must have at least one water cell in its neighbourhood.
                if (elevations[(y - 1) * _worldWidth + x] >=
                    _elevationCfg.LandElevationThreshold // north
                    && elevations[y * _worldWidth + x + 1] >=
                    _elevationCfg.LandElevationThreshold // east
                    && elevations[(y + 1) * _worldWidth + x] >=
                    _elevationCfg.LandElevationThreshold // south
                    && elevations[y * _worldWidth + (x - 1)] >=
                    _elevationCfg.LandElevationThreshold)
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

                coastalSlopes[neighY * _worldWidth + neighX] =
                    Math.Min(elevation + 1, _elevationCfg.MaxElevation);

                if (elevation < _elevationCfg.SnowThreshold - 1)
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
                    var direction = WorldMath.GetWrappedVector(pA, pB);
                    if (direction.LengthSquared() < 0.0001f) continue;

                    var normal = Vector2.Normalize(direction);
                    var relativeMotion = WorldMath.GetWrappedVector(plateMotions[neighbourPlate],
                        plateMotions[currentPlate]);
                    var stress = Vector2.Dot(relativeMotion, normal);
                    // Negative stress means they are crashing.
                    var convergence = MathF.Max(0f, -stress);
                    // Positive stress means they are pulling apart.
                    var divergence = MathF.Max(0f, stress);
                    // Only true if both are continents.
                    var continentalInteraction =
                        plateTypes[currentPlate] && plateTypes[neighbourPlate];
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
                            minMixedConvergence =
                                MathF.Min(minMixedConvergence, convergence * 4.5f);
                            maxMixedConvergence =
                                MathF.Max(maxMixedConvergence, convergence * 4.5f);
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
    }

    /// <summary>
    ///     Assign hotspots to certain cells in the world.
    /// </summary>
    private void GenerateHotspots()
    {
        var hotspots = _hotspotMap.Memory.Span;
        hotspots.Clear();
        var voronoiTypes = _voronoiCellTypes.Memory.Span;
        var voronoiCenters = _voronoiCells.Memory.Span;
        var plateMotions = _plateMotions.Memory.Span;
        var noiseMap = _noiseMap.Memory.Span;
        // +1 because Next upper bound is exclusive
        var chainCount = _rng.Next(_volcanicCfg.MinIslandChains, _volcanicCfg.MaxIslandChains + 1);

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
            var chainLength =
                _rng.Next(_volcanicCfg.MinChainLength, _volcanicCfg.MaxChainLength + 1);

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
                var offsetX = (int)(chainDirection.X * i * _volcanicCfg.ChainSpacing);
                var offsetY = (int)(chainDirection.Y * i * _volcanicCfg.ChainSpacing);
                var centerX = WorldMath.WrapX(startX + offsetX);
                var centerY = startY + offsetY;

                // Keep within bounds with some wrapping
                // centerX = (centerX % worldWidth + worldWidth) % worldWidth;
                // centerY = (centerY % worldHeight + worldHeight) % worldHeight;
                if (centerY < 0 || centerY >= _worldHeight) continue;

                var radius = _rng.Next(_volcanicCfg.MinHotspotRadius,
                    _volcanicCfg.MaxHotspotRadius + 1);
                var strength =
                    (float)(_rng.NextDouble() * (_volcanicCfg.MaxHotspotStrength -
                                                 _volcanicCfg.MinHotspotStrength) +
                            _volcanicCfg.MinHotspotStrength);

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
                            _volcanicCfg.MaxHotspotStrength - 2)
                            hotspots[y * _worldWidth + x] = 0;
                        else
                            hotspots[y * _worldWidth + x] += coneHeight;

                        _minHotspotHeight = Math.Min(_minHotspotHeight, coneHeight);
                        _maxHotspotHeight = Math.Max(_maxHotspotHeight, coneHeight);
                    }
            }
        }
    }

    private void ApplyTectonics()
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


        var land = 0f;
        var water = 0f;
        // Apply noise and slopes to elevation map.
        for (var i = 0; i < _worldWidth * _worldHeight; i += 1)
        {
            // Continental plates (>=4) get higher base, oceanic (<4) get lower for deep trenches.
            var slopeFactor = coastalSlopes[i] / _coastalCfg.MaxCoastalSlope;
            var normalizedNoise = noiseField[i];
            var tectonicD = tectonicDelta[i];
            var hotspot = hotspots[i];

            // For oceanic plates, reduce noise impact to allow deeper trenches
            // var cellElevationContribution = elevations[i] >= LandElevationThreshold
            //     ? elevations[i]
            //     : 0;
            var noiseMultiplier = elevations[i] >= _elevationCfg.LandElevationThreshold
                ? 1.0f
                : -1.0f;

            var elevation = // cellElevationContribution +
                elevations[i] +
                slopeFactor *
                normalizedNoise * noiseMultiplier * elevations[i]; // *
            // var tectonic = (MaxElevation - elevation) * (tectonicD / _maxTectonicDelta);
            elevation = Math.Min(_elevationCfg.MaxElevation, elevation + tectonicD + hotspot);
            // Only apply hotspots if max elevation is not exceeded.
            // This should not happen in most cases as hotspots are supposed to be generated
            // in oceanic tiles.
            // if (hotspot > 0 && elevation + hotspot < MaxElevation) elevation += hotspot;

            // Store as float, don't clamp yet
            elevations[i] = Math.Min(_elevationCfg.MaxElevation, elevation);

            if (elevations[i] >= _elevationCfg.LandElevationThreshold)
            {
                land++;
            }
            else
            {
                water++;
            }
        }

        var ratio = land / (water + land);
        Console.WriteLine($"Tectonics: {ratio * 100} % land");
    }

    #endregion

    #region Climate and Biomes

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
                if (elevations[idx] < _elevationCfg.LandElevationThreshold)
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
    /// Generates a wind direction field influenced by global bands (Hadley cells,
    /// trade winds, westerlies, polar easterlies), small-scale noise and elevation.
    /// Results are stored in `_windDirections` as discrete (-1,0,1) integer vectors.
    /// </summary>
    private void CalculateWindField()
    {
        var elevations = _elevation.Memory.Span;
        var noiseMap = _noiseMap.Memory.Span;
        var windDirections = _windDirections.Memory.Span;
        var windSpeeds = _windSpeeds.Memory.Span;

        // Compute max elevation for normalization (avoid using _maxElevation which is set later)
        var maxElev = float.MinValue;
        var total = _worldWidth * _worldHeight;
        for (var i = 0; i < total; i++) maxElev = MathF.Max(maxElev, elevations[i]);
        if (maxElev <= 0) maxElev = 1f;

        var mid = _worldHeight / 2f;

        for (var y = 0; y < _worldHeight; y++)
        {
            // Absolute latitude 0 at equator -> 1 at poles
            var latitudeAbs = MathF.Abs(y - mid) / mid;

            // Band selection: trade winds (near equator) and westerlies (mid-latitudes)
            // and polar easterlies (near poles). This mirrors common atmospheric cells.
            var bandDx = latitudeAbs < 0.33f || latitudeAbs >= 0.66f ? -1f : 1f;

            // Hemispheric sign: north (y < mid) => -1, south (y > mid) => +1, equator => 0
            var hemisphere = MathF.Sign(y - mid);

            // Meridional component: trades blow toward the equator, westerlies toward poles
            var bandDy = bandDx < 0 ? -hemisphere : hemisphere;

            for (var x = 0; x < _worldWidth; x++)
            {
                var idx = y * _worldWidth + x;

                // Local noise to add small-scale variation
                var localNoise = (noiseMap[idx] * 2f - 1f) * 0.25f; // approx -0.25..0.25

                // Base floating vector
                var fx = bandDx + localNoise;
                var fy = bandDy + localNoise * 0.5f;

                // Slow down wind with elevation: higher elevation -> reduced magnitude
                var elev = elevations[idx];
                var elevNorm = Math.Clamp(elev / maxElev, 0f, 1f);
                var slowdown = elevNorm * 0.8f; // up to 80% slowdown on highest peaks
                fx *= (1f - slowdown);
                fy *= (1f - slowdown);

                // Threshold to consider the flow effectively calm
                const float calmThreshold = 0.33f;

                var finalDx = MathF.Abs(fx) < calmThreshold ? 0 : Math.Sign(fx);
                var finalDy = MathF.Abs(fy) < calmThreshold ? 0 : Math.Sign(fy);

                // Occasional local blocking: very steep local slopes reduce wind to calm
                // Check simple slope with immediate west/east neighbor
                var left = elevations[y * _worldWidth + WorldMath.WrapX(x - 1)];
                var right = elevations[y * _worldWidth + WorldMath.WrapX(x + 1)];
                var slope = MathF.Abs(right - left);
                if (slope > (_elevationCfg.MaxElevation * 0.5f))
                {
                    finalDx = 0;
                }

                // Store discrete wind direction
                windDirections[idx] = (finalDx, finalDy);

                // Compute a simple magnitude (pre-discretization) and quantize to 0..255
                var mag = MathF.Sqrt(fx * fx + fy * fy);
                // normalize by a heuristic max (2.0f covers bandDx +- noise)
                var normalized = Math.Clamp(mag / 2f, 0f, 1f);
                var speedByte = (byte)(normalized * 255f);

                // If blocked to calm, zero the speed
                if (finalDx == 0 && finalDy == 0) speedByte = 0;

                windSpeeds[idx] = speedByte;
            }
        }
    }
    
    private float[,] CalculatePhysicalRainfall()
    {
        var elevations = _elevation.Memory.Span;
        var noiseMap = _noiseMap.Memory.Span;
        var riverMap = _riverMap.Memory.Span;
        var windDirections = _windDirections.Memory.Span;
        var windSpeeds = _windSpeeds.Memory.Span;
        var rainfall = new float[_worldWidth, _worldHeight];
        const float moistureRechargeRate = 0.09f;
        const float rainDropFactor = 0.06f;

        (int, int)[] directions =
            [(0, -1), (1, 0), (0, 1), (-1, 0), (1, -1), (1, 1), (-1, 1), (-1, -1)];

        for (var y = 0; y < _worldHeight; y++)
        {
            // 1. Warped Latitude: Prevents perfectly horizontal "shear" lines
            // We use the y-coordinate + a tiny bit of noise based on y to wiggle the bands
            var latWarp = noiseMap[y * _worldWidth] * 2.04f;
            var latitude = MathF.Abs(y - _worldHeight / 2f) / (_worldHeight / 2f) + latWarp;

            // Determine predominant row-wise wind direction from generated wind field.
            var bandWindDir = latitude is < 0.33f or >= 0.66f ? -1 : 1;
            var sumX = 0;
            for (var sx = 0; sx < _worldWidth; sx++)
                sumX += windDirections[y * _worldWidth + sx].Item1;

            var windDir = Math.Sign(sumX);
            if (windDir == 0) windDir = bandWindDir;

            // 2. Priming: Start with a base, but don't write to the map yet
            var cloudMoisture = _riverCfg.RainfallLandBase;

            // Sweep 2x the width. Pass 1 'primes' the moisture; Pass 2 'records' it.
            for (var step = 0; step < _worldWidth * 2; step++)
            {
                // windX moves East or West based on predominant row windDir
                var windX = WorldMath.WrapX(windDir == 1 ? step : _worldWidth - step);
                var idx = y * _worldWidth + windX;
                var elev = elevations[idx];
                var isSecondPass = step >= _worldWidth;

                if (elev < _elevationCfg.LandElevationThreshold)
                {
                    // Ocean Recharge
                    cloudMoisture = MathF.Min(_riverCfg.RainfallOceanBase,
                        cloudMoisture + moistureRechargeRate);
                    if (isSecondPass)
                        rainfall[windX, y] = _riverCfg.RainfallOceanBase; // Base ocean rain
                }
                else
                {
                    // Use the per-cell wind vector where available to find the upwind neighbor.
                    var cellWindX = windDirections[idx].Item1;
                    var dx = cellWindX != 0 ? cellWindX : windDir;
                    var prevX = WorldMath.WrapX(windX - dx);
                    var lift = elev - elevations[y * _worldWidth + prevX];

                    // Make it wet around rivers.
                    foreach (var (dirX, dirY) in directions)
                    {
                        var nx = WorldMath.WrapX(windX + dirX);
                        var ny = y + dirY;
                        if (ny > 0 && ny < _worldHeight && riverMap[ny * _worldWidth + nx])
                            cloudMoisture = _riverCfg.RainfallOceanBase;
                    }

                    float rainDropped = 0;
                    if (lift > 0)
                    {
                        // Squeeze moisture out on the windward side
                        rainDropped = MathF.Min(cloudMoisture,
                            lift * cloudMoisture * rainDropFactor);
                        cloudMoisture -= rainDropped;
                    }

                    if (isSecondPass)
                        // Ambient rain + Orographic (mountain) rain
                        rainfall[windX, y] = cloudMoisture * 0.15f + rainDropped * 2.0f;

                    // Deserts happen here: naturally lose moisture over land
                    cloudMoisture *= 0.992f;
                }
            }
        }

        return rainfall;
    }

    /// <summary>
    ///     Generates world maps for various climate features.
    /// </summary>
    private void GenerateClimate()
    {
        // 1. Initialise Spans
        var elevations = _elevation.Memory.Span;
        var temperature = _temperature.Memory.Span;
        var humidity = _humidity.Memory.Span;
        var riverMap = _riverMap.Memory.Span;
        var strahlerRiver = _strahlerRiver.Memory.Span;
        var biomes = _biomes.Memory.Span;
        var noiseMap = _noiseMap.Memory.Span;

        // 2. Step One: Generate the "Baked" Rainfall Map (with Shadows)
        // We do this first because River Flow AND Biomes depend on it.
        var rainfallMap = CalculatePhysicalRainfall();

        // 3. Step Two: Calculate Temperature and Relative Humidity
        for (var y = 0; y < _worldHeight; y++)
        {
            // 1. Warped Latitude: Prevents perfectly horizontal "shear" lines
            // We use the y-coordinate + a tiny bit of noise based on y to wiggle the bands
            var noise = 1 - noiseMap[y * _worldWidth] * 2; // normalise to (-1,1)
            var latWarp = noise * 5.04f;
            var latitude = MathF.Abs(y + latWarp - _worldHeight / 2f) / (_worldHeight / 2f);
            var latitudeFactor = (float)Math.Pow(latitude, 1.1);
            var baseTemp = _climateCfg.BaseTempMax -
                           (_climateCfg.BaseTempMax - _climateCfg.BaseTempMin) * latitudeFactor;

            for (var x = 0; x < _worldWidth; x++)
            {
                var idx = y * _worldWidth + x;
                var elevationFactor =
                    Math.Clamp(elevations[idx] / _elevationCfg.MaxElevation, 0f, 1f);

                // Temperature with Lapse Rate
                temperature[idx] = baseTemp + elevationFactor * 10;

                // Get the rainfall we calculated in Step 1
                var absoluteMoisture = rainfallMap[x, y];

                // Add local River Humidity (The "Nile Greenery" effect)
                if (riverMap[idx]) absoluteMoisture += 0.55f;

                // Calculate Relative Humidity (Carrying Capacity logic)
                var normalizedTemp = Math.Clamp((temperature[idx] + 50f) / 100f, 0.01f, 1.0f);
                var carryingCapacity = MathF.Pow(normalizedTemp, 2.0f);

                // This is the final humidity value passed to the Biome selector
                humidity[idx] = Math.Clamp(absoluteMoisture / carryingCapacity, 0.0f, 1.0f);

                // 4. Step Three: Determine Biome
                biomes[idx] = DetermineBiome(
                    temperature[idx],
                    humidity[idx],
                    elevations[idx],
                    elevations[idx] < _elevationCfg.LandElevationThreshold,
                    riverMap[idx],
                    strahlerRiver[idx]);
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
    /// <param name="isRiver">True if the cell is river, false otherwise.</param>
    /// <param name="strahlerOrder">Strahler order for river cells.</param>
    /// <returns>Biome of the cell.</returns>
    private Biome DetermineBiome(
        float temp,
        float relHumidity,
        float elevation,
        bool isWater,
        bool isRiver,
        int strahlerOrder)
    {
        if (isWater)
            if (elevation < _elevationCfg.HighSeaThreshold)
            {
                return Biome.HighSeas;
            }
            else if (elevation < _elevationCfg.OceanThreshold)
            {
                return Biome.Ocean;
            }
            else if (elevation < _elevationCfg.ShelfThreshold)
            {
                return Biome.Shelf;
            }
            else if (elevation < _elevationCfg.ShallowsThreshold)
            {
                return Biome.Shallows;
            }

        if (isRiver)
            return strahlerOrder switch
            {
                <= 2 => Biome.Creek,
                <= 4 => Biome.MinorRiver,
                _ => Biome.MajorRiver
            };


        // High Altitude "Dead Zone" (Above the Tree Line)
        if (elevation > _elevationCfg.HighMountainThreshold)
            return temp < 0f ? Biome.Glacier : Biome.RockPeak;

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
            case < 5f when elevation > _volcanicCfg.CraterElevationThreshold:
                return Biome.AlpineTundra;
            // Differentiate between standard Boreal forest and heavy precipitation zones
            case < 5f:
                return relHumidity > 0.65f ? Biome.SnowyForest : Biome.Taiga;
            // 5. Temperate
            case < 22f when relHumidity < 0.15f:
                return Biome.ColdDesert;
            case < 22f when elevation > _volcanicCfg.CraterElevationThreshold && relHumidity > 0.6f:
                return Biome.HighlandMoor;
            case < 22f when relHumidity < 0.4f:
                return Biome.Steppe;
            case < 22f when relHumidity < 0.6f:
                return Biome.Grassland;
            case < 22f:
                return Biome.TemperateForest; // High humidity temperate zones
        }

        // 6. Tropical / Hot
        // High altitude tropics create unique "Cloud Forests" (very high humidity + altitude)
        if (elevation > _volcanicCfg.CraterElevationThreshold && relHumidity > 0.75f)
            return Biome.CloudForest;

        return relHumidity switch
        {
            < 0.15f => Biome.HotDesert,
            < 0.45f => Biome.Savanna,
            < 0.5f => Biome.TropicalSeasonalForest,
            _ => Biome.TropicalRainforest
        };
    }

    #endregion

    #region Erosion

    /// <summary>
    ///     Apply erosion with hydraulic simulation, sediment transport and thermal erosion.
    /// </summary>
    private void ApplyErosion()
    {
        var elevationsF = _elevation.Memory.Span;
        var humidity = _humidity.Memory.Span;
        var hotspots = _hotspotMap.Memory.Span;

        // 1. Allocate arrays ONCE outside the loop (Double Buffering)
        var terrain = new float[_worldWidth * _worldHeight];
        var nextTerrain = new float[_worldWidth * _worldHeight];

        var water = new float[_worldWidth * _worldHeight];
        var nextWater = new float[_worldWidth * _worldHeight];

        var sediment = new float[_worldWidth * _worldHeight];
        var nextSediment = new float[_worldWidth * _worldHeight];

        // Initialize terrain
        elevationsF.CopyTo(terrain);

        // Pre-calculate wrapped coordinates for performance
        var leftX = new int[_worldWidth];
        var rightX = new int[_worldWidth];
        for (var x = 0; x < _worldWidth; x++)
        {
            leftX[x] = WorldMath.WrapX(x - 1);
            rightX[x] = WorldMath.WrapX(x + 1);
        }

        for (var iter = 0; iter < _erosionCfg.ErosionIterations; iter++)
        {
            // Copy current state to 'next' state for delta accumulation
            Array.Copy(terrain, nextTerrain, terrain.Length);
            Array.Copy(water, nextWater, water.Length);
            Array.Copy(sediment, nextSediment, sediment.Length);

            // --- STEP 1: Pipeline-Integrated Rainfall ---
            for (var i = 0; i < _worldWidth * _worldHeight; i++)
            {
                // Reuse the humidity map generated by GenerateClimate(). 
                // 0 humidity = 10% base rain, 1.0 humidity = 110% rain.
                var localRainRate = _erosionCfg.RainRate * (0.1f + humidity[i]);
                water[i] += localRainRate;
                nextWater[i] += localRainRate; // Ensure next state has the rain too
            }

            // --- STEP 2 & 3: Hydraulic Flow & Sediment Transport ---
            // Y goes from 1 to Height-1 (assuming poles don't wrap), X wraps completely.
            for (var y = 1; y < _worldHeight - 1; y++)
            {
                for (var x = 0; x < _worldWidth; x++)
                {
                    var index = y * _worldWidth + x;
                    var currentWater = water[index];

                    if (currentWater <= 0) continue;

                    // // The Ocean Sink
                    // // If this cell is underwater, the ocean absorbs the water and dissolves the sediment.
                    // // (Assuming you use LandElevationThreshold to define sea level like in your FillDepressions method)
                    if (terrain[index] < _elevationCfg.LandElevationThreshold)
                    {
                        nextWater[index] = 0f;
                        nextSediment[index] = 0f;
                        continue;
                    }

                    // Correct gradient math using wrapped neighbors
                    var lX = leftX[x];
                    var rX = rightX[x];

                    // Central difference: (Right - Left) / 2
                    var slopeX = (terrain[y * _worldWidth + rX] - terrain[y * _worldWidth + lX]) *
                                 0.5f;
                    var slopeY = (terrain[(y + 1) * _worldWidth + x] -
                                  terrain[(y - 1) * _worldWidth + x]) * 0.5f;

                    var slope = MathF.Sqrt(slopeX * slopeX + slopeY * slopeY);

                    var velocity = slope < _erosionCfg.MinSlope
                        ? 0f
                        : MathF.Sqrt(_erosionCfg.Gravity * slope) * currentWater;
                    var capacity = _erosionCfg.SedimentCapacity * velocity * currentWater;
                    var currentSediment = sediment[index];

                    if (currentSediment > capacity)
                    {
                        // Deposit
                        var depositAmount =
                            (currentSediment - capacity) * _erosionCfg.DepositionRate;
                        nextTerrain[index] += depositAmount;
                        nextSediment[index] -= depositAmount;
                    }
                    else
                    {
                        // Erode
                        var erodeAmount = (capacity - currentSediment) *
                                          _erosionCfg.HydraulicErosionRate;

                        // Pipeline reuse: Hotspots resist erosion
                        if (hotspots[index] > 0.1f) erodeAmount *= _volcanicCfg.VolcanicResistance;

                        erodeAmount = MathF.Min(erodeAmount, terrain[index] * 0.1f);
                        nextTerrain[index] -= erodeAmount;
                        nextSediment[index] += erodeAmount;
                    }

                    // Outflow routing
                    var totalOutflow = 0f;
                    (int dx, int dy)[] directions = [(0, -1), (1, 0), (0, 1), (-1, 0)];

                    foreach (var (dx, dy) in directions)
                    {
                        var nx = WorldMath.WrapX(x + dx);
                        var ny = y + dy;

                        if (ny < 0 || ny >= _worldHeight) continue;

                        var neighborIndex = ny * _worldWidth + nx;
                        var neighborSlope = terrain[index] - terrain[neighborIndex];

                        if (neighborSlope <= 0) continue;

                        var outflow = currentWater * neighborSlope *
                                      _erosionCfg.HydraulicErosionRate;

                        // Distribute to next state
                        nextWater[neighborIndex] += outflow * 0.25f;
                        nextSediment[neighborIndex] += currentSediment * outflow /
                            MathF.Max(currentWater, _erosionCfg.WaterViscosity) * 0.25f;
                        totalOutflow += outflow;
                    }

                    nextWater[index] -= totalOutflow;
                    nextSediment[index] -= currentSediment * totalOutflow /
                                           MathF.Max(currentWater, 0.001f);
                }
            }

            // Swap buffers before thermal erosion so it acts on hydraulically updated terrain
            var tempT = terrain;
            terrain = nextTerrain;
            nextTerrain = tempT;
            (sediment, nextSediment) = (nextSediment, sediment);
            (water, nextWater) = (nextWater, water);

            // Reset nextTerrain to current for thermal modifications
            Array.Copy(terrain, nextTerrain, terrain.Length);

            // --- STEP 4: Thermal Erosion (Avalanche) ---
            for (var y = 1; y < _worldHeight - 1; y++)
            {
                for (var x = 0; x < _worldWidth; x++)
                {
                    var index = y * _worldWidth + x;
                    var currentHeight = terrain[index]; // READ from static state

                    // Ignore water terrain.
                    if (currentHeight < _elevationCfg.LandElevationThreshold) continue;

                    var lowestNeighborHeight = currentHeight;
                    var lowestIndex = index;

                    // 8-way check with wrapping
                    (int dx, int dy)[] directions =
                        [(0, -1), (1, 0), (0, 1), (-1, 0), (1, -1), (1, 1), (-1, 1), (-1, -1)];
                    foreach (var (dx, dy) in directions)
                    {
                        var nx = WorldMath.WrapX(x + dx);
                        var ny = y + dy;
                        if (ny < 0 || ny >= _worldHeight) continue;

                        var nIndex = ny * _worldWidth + nx;
                        var neighborHeight = terrain[nIndex];

                        if (neighborHeight >= lowestNeighborHeight) continue;
                        lowestNeighborHeight = neighborHeight;
                        lowestIndex = nIndex;
                    }

                    var slope = currentHeight - lowestNeighborHeight;
                    if (slope <= _erosionCfg.TalusAngle) continue;

                    var slideAmount =
                        MathF.Min(_erosionCfg.ThermalErosionRate * slope, slope * 0.5f);
                    // WRITE to dynamic state
                    nextTerrain[index] -= slideAmount;
                    nextTerrain[lowestIndex] += slideAmount;
                }
            }

            // Final swap for this iteration
            tempT = terrain;
            terrain = nextTerrain;
            nextTerrain = tempT;

            // --- STEP 5: Pipeline-Integrated Evaporation ---
            for (var i = 0; i < _worldWidth * _worldHeight; i++)
            {
                // Lower humidity = higher evaporation
                var climateModifier = 2.0f - humidity[i];
                var adjustedEvaporation = _erosionCfg.EvaporationRate * climateModifier;
                water[i] = MathF.Max(0, water[i] - adjustedEvaporation);
            }
        }

        // Apply the final terrain back to the global elevation span
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
        var rainfall = CalculatePhysicalRainfall();
        ApplyRainShadows(rainfall);

        // Step 2: Calculate flow directions (steepest downhill neighbor)
        CalculateFlowDirections();

        // Step 3: Accumulate flow from upstream cells
        var flowAccumulation = AccumulateFlow(rainfall);
        CalculateStrahlerOrder(flowAccumulation);

        // Step 4: Carve rivers where flow accumulation is high enough
        CarveRivers(flowAccumulation);
    }

    /// <summary>
    ///     Adjusts the rainfall map based on global wind bands (Trade Winds, Westerlies) 
    ///     and terrain height to simulate realistic rain shadows.
    /// </summary>
    private void ApplyRainShadows(in float[,] rainfall)
    {
        var elevationsF = _elevation.Memory.Span;
        var windDirections = _windDirections.Memory.Span;
        var windSpeeds = _windSpeeds.Memory.Span;

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
            var bandSign = latitude is < 0.33f or >= 0.66f ? -1 : 1;
            // Aggregate per-row wind x-component to choose sweep order. If calm,
            // fall back to band-based sign.
            var sumX = 0;
            for (var sx = 0; sx < _worldWidth; sx++) sumX += windDirections[y * _worldWidth + sx].Item1;
            var windDir = Math.Sign(sumX);
            if (windDir == 0) windDir = bandSign;

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

                if (elevation < _elevationCfg.LandElevationThreshold)
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
                    // DISCHARGE: Use per-cell wind to find the upwind neighbor.
                    var cellWindX = windDirections[y * _worldWidth + windX].Item1;
                    // TODO: Possibly add wind speed to propagate further
                    var dx = cellWindX != 0 ? cellWindX : windDir;
                    var prevX = WorldMath.WrapX(windX - dx);
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
                if (elevation < _elevationCfg.LandElevationThreshold || y == 0 ||
                    y == _worldHeight - 1)
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
                    var minNeighborWaterLevel = float.MaxValue;

                    // Check all 8 neighbors
                    // for (var dy = -1; dy <= 1; dy++)
                    // for (var dx = -1; dx <= 1; dx++) 
                    (int, int)[] directions = [(0, -1), (1, 0), (0, 1), (-1, 0)];
                    foreach (var (dx, dy) in directions)
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

    /// <summary>
    ///     Calculates a map of hypothetical water flow directions for each cell.
    /// </summary>
    /// <returns>A grid of flow vectors per cell.</returns>
    private void CalculateFlowDirections()
    {
        var elevationsF = _elevation.Memory.Span;
        var flowDirections = _flowDirections.Memory.Span;

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

                    var idx = ny * _worldWidth + nx;
                    var neighborElevation = elevationsF[idx];
                    var drop = currentElevation - neighborElevation;
                    // if (dx != 0 && dy != 0) 
                    //     drop /= 1.4142135f; // Normalizes diagonal drop rate

                    if (drop <= steepestDrop) continue;

                    steepestDrop = drop;
                    bestDirection = (dx, dy);
                }

                flowDirections[y * _worldWidth + x] = bestDirection;
            }
    }
    
    /// <summary>
    ///     Simulate the flow of water down the elevations and keep track of where it ends up.
    /// </summary>
    /// <param name="flowDirections">Direction of flow for each cell.</param>
    /// <param name="rainfall">Mapping of rainfall amount per cell.</param>
    /// <returns>Map of accumulated flow for each cell.</returns>
    private float[,] AccumulateFlow(in float[,] rainfall)
    {
        var flowDirections = _flowDirections.Memory.Span;
        var flowAccumulation = new float[_worldWidth, _worldHeight];
        var inDegree = new int[_worldWidth, _worldHeight];
        var elevations = _elevation.Memory.Span;

        // Step 1: Initialize rainfall and calculate In-Degrees
        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
            {
                flowAccumulation[x, y] = rainfall[x, y];

                var (dx, dy) = flowDirections[y * _worldWidth + x];
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
            var (dx, dy) = flowDirections[y * _worldWidth + x];

            if (dx == 0 && dy == 0) continue;

            var nx = WorldMath.WrapX(x + dx);
            var ny = y + dy;

            if (ny < 0 || ny >= _worldHeight) continue;

            var currentVolume = flowAccumulation[x, y];
            var waterToPass = 0f;

            // --- The Ocean Sink ---
            // If the current cell is in the ocean, it absorbs the water. 
            // We do NOT pass water to the next ocean cell.
            var isOcean = elevations[y * _worldWidth + x] < _elevationCfg.LandElevationThreshold;
            if (!isOcean)
            {
                // Ground Seepage (Absolute loss per tile)
                // Keep this small (e.g., 0.05f) so it only kills tiny streams, not main rivers.
                const float groundSeepage = 0.05f;

                // Surface Evaporation (Scales with the square root of volume)
                var aridity = 1.0f - rainfall[x, y];
                const float evaporationFactor = 0.05f; // Tweak this based on map size

                // A river of volume 100 loses 10 * 0.05 = 0.5 volume.
                // A river of volume 10,000 loses 100 * 0.05 = 5.0 volume. (Much more survivable!)
                var evaporationLoss = MathF.Sqrt(currentVolume) * aridity * evaporationFactor;

                // Calculate remaining water
                waterToPass = currentVolume - groundSeepage - evaporationLoss;
            }


            // Ensure we don't pass negative water (ocean cells pass 0).
            if (waterToPass > 0) flowAccumulation[nx, ny] += waterToPass;

            // Mark that one upstream dependency is resolved.
            // We MUST still decrement the neighbor's inDegree even if we pass 0 water,
            // otherwise the topological sort will freeze and skip the rest of the map.
            inDegree[nx, ny]--;

            // If all upstream dependencies are resolved, this cell is ready to flow
            if (inDegree[nx, ny] == 0) queue.Enqueue((nx, ny));
        }

        return flowAccumulation;
    }

    /// <summary>
    ///     Calculates the Strahler Stream Order for the entire river network.
    /// </summary>
    /// <returns>
    ///     An integer matrix with the following valuation:
    ///     - Order 1-2: small creeks
    ///     - Order 3-4: small rivers
    ///     - Order  5+: major rivers
    /// </returns>
    private void CalculateStrahlerOrder(float[,] flowAccumulation)
    {
        var flowDirections = _flowDirections.Memory.Span;
        var strahlerRiver = _strahlerRiver.Memory.Span;
        strahlerRiver.Clear(); // Critical: reset before calculation

        var inDegree = new int[_worldWidth, _worldHeight];
        var maxIncomingOrder = new int[_worldWidth, _worldHeight];
        var countOfMaxOrder = new int[_worldWidth, _worldHeight];

        // 1. Minimum volume required to even be considered a "stream"
        const float minVolumeThreshold = 0.51f;

        // Calculate in-degrees
        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
            {
                // Only count in-degree if the upstream cell actually has water!
                if (flowAccumulation[x, y] < minVolumeThreshold) continue;

                var (dx, dy) = flowDirections[y * _worldWidth + x];
                if (dx == 0 && dy == 0) continue;
                var nx = WorldMath.WrapX(x + dx);
                var ny = y + dy;
                if (ny >= 0 && ny < _worldHeight) inDegree[nx, ny]++;
            }

        // 2. Queue the sources
        var queue = new Queue<(int, int)>();
        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
                // A source is a cell with water but no water flowing into it
                if (inDegree[x, y] == 0 && flowAccumulation[x, y] >= minVolumeThreshold)
                {
                    strahlerRiver[y * _worldWidth + x] = 1;
                    queue.Enqueue((x, y));
                }

        var maxOrder = float.MinValue;
        // 3. Process topologically
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            var currentOrder = strahlerRiver[y * _worldWidth + x];

            var (dx, dy) = flowDirections[y * _worldWidth + x];
            if (dx == 0 && dy == 0)
            {
                continue;
            }

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
            if (inDegree[nx, ny] != 0) continue;
            // RESOLVE ORDER: If two or more tributaries of the same MAX order meet, level up.
            // Otherwise, inherit the max incoming order.
            strahlerRiver[ny * _worldWidth + nx] = countOfMaxOrder[nx, ny] >= 2
                ? maxIncomingOrder[nx, ny] + 1
                : maxIncomingOrder[nx, ny];

            maxOrder = MathF.Max(strahlerRiver[ny * _worldWidth + nx], maxOrder);

            queue.Enqueue((nx, ny));
        }
    }

    /// <summary>
    ///     Create a boolean river map of the world where true -> belongs to a river, false otherwise.
    /// </summary>
    /// <param name="flowAccumulation">Accumulated flow for each cell.</param>
    private void CarveRivers(float[,] flowAccumulation)
    {
        var elevationsF = _elevation.Memory.Span;
        var riverMap = _riverMap.Memory.Span;
        var strahlerRiver = _strahlerRiver.Memory.Span;
        riverMap.Clear();

        // Visual and Physical Thresholds
        const int minOrderToVisualise = 2;
        const int minOrderToCarve = 2;
        const float MinVolumeToCarve = 0.1f;

        // A single pass is all we need. Flow accumulation already guarantees continuity.
        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
            {
                var index = y * _worldWidth + x;
                var cellElevation = elevationsF[index];

                // 1. Skip immediately if we are already in the ocean
                if (cellElevation < _elevationCfg.LandElevationThreshold)
                    continue;

                var order = strahlerRiver[index];
                var volume = flowAccumulation[x, y];

                // 2. Skip if this cell doesn't meet the requirements for a river
                if (volume < MinVolumeToCarve || order < minOrderToCarve)
                    continue;

                // --- THE STRAHLER UPGRADE ---
                // A. Visual Overlay: Only show the big boys
                if (order >= minOrderToVisualise) riverMap[index] = true;

                // B. Physical Carving: Depth scales with ACTUAL WATER VOLUME
                var erosionPower = MathF.Pow(volume, 0.1f) * 0.01f; // Tweak exponents to taste
                var targetDepth = MathF.Min(_riverCfg.RiverMaxCarveDepth,
                    erosionPower * _riverCfg.RiverCarveScale);

                // C. Apply the carve safely
                // We ensure we never carve below your designated min elevation AND never accidentally carve below sea level
                var minAllowedElevation = MathF.Max(_riverCfg.RiverCarveMinElevation,
                    _elevationCfg.LandElevationThreshold);
                var carvedElevation = cellElevation - targetDepth;

                elevationsF[index] = MathF.Max(minAllowedElevation, carvedElevation);
            }
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
                case < 0.1f when !riverMap[i] && elevation >= _elevationCfg.LandElevationThreshold:
                    surfaceMap[i] = SurfaceFeature.Glacier;
                    continue;
                // 2. Check for Snow (Slightly warmer than glaciers, or high mountains)
                case < 0.25f when elevation >= _elevationCfg.LandElevationThreshold:
                    surfaceMap[i] = SurfaceFeature.Snow;
                    continue;
            }

            // 3. Check for Mountains (Purely geographical)
            if (elevation >= _elevationCfg.HighMountainThreshold)
            {
                surfaceMap[i] = SurfaceFeature.Mountain;
                continue;
            }

            if (riverMap[i])
            {
                surfaceMap[i] = SurfaceFeature.River;
                continue;
            }

            if (hotspotMap[i] > _volcanicCfg.LavaHotspotThreshold &&
                elevation >= _elevationCfg.LandElevationThreshold)
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
                    if (elevations[y * _worldWidth + x] < _elevationCfg.LandElevationThreshold)
                        continue;
                    // Skip tiles with features already defined on them.
                    if (surfaceMap[y * _worldWidth + x] != SurfaceFeature.None) continue;

                    surfaceMap[y * _worldWidth + x] = SurfaceFeature.Mountain;
                }
            }
    }

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
                if (visited[x, y] ||
                    hotspotMap[y * _worldWidth + x] < _volcanicCfg.HotspotMinStrength) continue;

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

        var craterElevationThreshold = _volcanicCfg.CraterElevationThreshold;

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
            if (elevation >= _volcanicCfg.CraterElevationThreshold &&
                volcanoType is SurfaceFeature.Stratovolcano or SurfaceFeature.Shield &&
                strength >= 0.8f
               )
            {
                surfaceMap[centerY * _worldWidth + centerX] = SurfaceFeature.Crater;
            }
            else if (elevation >= _volcanicCfg.CinderElevationThreshold &&
                     volcanoType == SurfaceFeature.Cinder)
            {
                surfaceMap[centerY * _worldWidth + centerX] = SurfaceFeature.Cinder;
            }

            ;
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
                if (existing is SurfaceFeature.River or SurfaceFeature.Glacier or
                    SurfaceFeature.Lava)
                    continue;

                surfaceMap[y * _worldWidth + wrappedX] = volcanoType switch
                {
                    SurfaceFeature.Stratovolcano => normalizedDist switch
                    {
                        // Stratovolcanoes: steep, explosive, with ash and lava
                        < 0.4f when elevation >= _volcanicCfg.CalderaElevationThreshold =>
                            SurfaceFeature
                                .Stratovolcano,
                        > 0.3f and < 0.8f when elevation >= 5 => SurfaceFeature.Ash,
                        _ => surfaceMap[y * _worldWidth + wrappedX]
                    },
                    SurfaceFeature.Shield => normalizedDist switch
                    {
                        // Shield volcanoes: broad, gentle slopes, mostly lava
                        < 0.6f when elevation >= _volcanicCfg.ShieldVolcanoThreshold =>
                            SurfaceFeature.Shield,
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
                    elevation >= _volcanicCfg.CalderaElevationThreshold)
                    surfaceMap[y * _worldWidth + wrappedX] = SurfaceFeature.Caldera;
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
                if (existing is SurfaceFeature.River or SurfaceFeature.Lava or
                    SurfaceFeature.Glacier)
                    continue;

                var elevation = elevationsF[y * _worldWidth + x];
                var isWater = elevation < _elevationCfg.LandElevationThreshold;

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
                            if (neighborElevation < _elevationCfg.LandElevationThreshold)
                                adjacentWater++;
                            else
                                maxAdjElevation = Math.Max(maxAdjElevation, neighborElevation);
                        }

                    if (adjacentWater > 0)
                    {
                        // How high is this coast above the water?
                        var slope = elevation - _elevationCfg.LandElevationThreshold - 1;

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
                            if (neighborElevation < _elevationCfg.LandElevationThreshold) continue;
                            adjacentLand++;
                            if (neighborElevation >= _elevationCfg.SnowThreshold - 1)
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
        var land = 0f;
        var water = 0f;

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
                        var elevationClamped =
                            Math.Clamp(sourceRow[i], 0f, _elevationCfg.MaxElevation);
                        destRow[i] = Convert.ToInt32(MathF.Floor(elevationClamped));

                        if (destRow[i] >= _elevationCfg.LandElevationThreshold)
                        {
                            land++;
                        }
                        else
                        {
                            water++;
                        }
                    }
                }

                // The chunk now holds a 'view' of the master buffer, not a unique array
                chunks[chunkIdx] =
                    new WorldElevationChunk(chunkIdx, chunkXIndex, chunkYIndex, chunk);
            }

        var ratio = land / (land + water);
        Console.WriteLine($"Final World Map: {ratio * 100} % land");

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

    private WorldPackedChunk[] ToPackedChunks()
    {
        var elevation = _elevation.Memory.Span;
        var humidity = _humidity.Memory.Span;
        var temperature = _temperature.Memory.Span;
        var biome = _biomes.Memory.Span;
        var surfaceFeatures = _surfaceFeatures.Memory.Span;
        // Flow and Wind directions
        var flowDirections = _flowDirections.Memory.Span;
        var windDirections = _windDirections.Memory.Span;
        var windSpeeds = _windSpeeds.Memory.Span;

        const int chunkSize = WorldMath.ChunkSize;
        const int chunksAcross = WorldMath.ChunksAcross;
        const int worldWidth = WorldMath.WorldWidth;
        const int worldHeight = WorldMath.WorldHeight;

        var chunks = new WorldPackedChunk[chunksAcross * (worldHeight / chunkSize)];
        // Temporary buffer to copy humidity as byte values.
        var elevationBuffer = new byte[chunkSize];
        var humidityBuffer = new byte[chunkSize];

        for (var cy = 0; cy < worldHeight; cy += chunkSize)
            for (var cx = 0; cx < worldWidth; cx += chunkSize)
            {
                var chunkXIndex = cx / chunkSize;
                var chunkYIndex = cy / chunkSize;
                var chunkIdx = chunkYIndex * chunksAcross + chunkXIndex;
                var chunk = new PackedTile[chunkSize * chunkSize];

                // Copy rows from world-layout to contiguous chunk-layout
                for (var ly = 0; ly < chunkSize; ly++)
                {
                    var sourceStart = (cy + ly) * worldWidth + cx;
                    var srcElevation = elevation.Slice(sourceStart, chunkSize);
                    var srcHumidityFloat = humidity.Slice(sourceStart, chunkSize);
                    var srcTemperature = temperature.Slice(sourceStart, chunkSize);
                    var srcBiome = biome.Slice(sourceStart, chunkSize);
                    var srcSurfaceFeatures = surfaceFeatures.Slice(sourceStart, chunkSize);
                    var srcFlowDir = flowDirections.Slice(sourceStart, chunkSize);
                    var srcWindDir = windDirections.Slice(sourceStart, chunkSize);
                    var srcWindSpeeds = windSpeeds.Slice(sourceStart, chunkSize);

                    for (var i = 0; i < srcHumidityFloat.Length; i++)
                    {
                        // Ensure that humidity floats are always in range (0,1)!
                        humidityBuffer[i] = Convert.ToByte(srcHumidityFloat[i] * 100);
                        var elevationClamped =
                            Math.Clamp(srcElevation[i], 0f, _elevationCfg.MaxElevation);
                        elevationBuffer[i] = Convert.ToByte(MathF.Floor(elevationClamped));
                    }

                    var destRow = chunk.AsSpan().Slice(ly * chunkSize, chunkSize);

                    WorldPacker.PackToSpan(
                        destRow,
                        srcBiome,
                        elevationBuffer,
                        srcTemperature,
                        humidityBuffer,
                        srcFlowDir,
                        srcWindDir,
                        srcWindSpeeds,
                        srcSurfaceFeatures);
                }

                // The chunk now holds a 'view' of the master buffer, not a unique array
                chunks[chunkIdx] =
                    new WorldPackedChunk(chunkIdx, chunkXIndex, chunkYIndex, chunk);
            }

        return chunks;
    }

    #endregion
}