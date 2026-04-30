using System.Diagnostics;
using System.Numerics;
using TermRTS.Examples.Greenery.Ecs.Component;

namespace TermRTS.Examples.Greenery.WorldGen;

// Refer to link below for a nice layered noise map implementation:
// https://github.com/SebLague/Procedural-Landmass-Generation/blob/master/Proc%20Gen%20E03/Assets/Scripts/Noise.cs

// TODO: Combine surface feature and biome into one enum?

public enum SurfaceFeature : byte
{
    None,
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
    Reef,
    PackIce,
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
    MajorRiver,
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
        // _rawArray = ArrayPool<T>.Shared.Rent(size);
        _rawArray = new T[size];

        // Wrap ONLY the size we need into Memory
        Memory = _rawArray.AsMemory(0, size);
    }

    public void Dispose()
    {
        // Return to the pool so it can be reused
        // ArrayPool<T>.Shared.Return(_rawArray);
    }
}

public record struct Point(int X, int Y)
{
}

public class CylinderWorld
{
    #region Fields

    private readonly Random _rng;
    private readonly int _seed;
    private readonly int _worldWidth;
    private readonly int _worldHeight;
    private readonly int _voronoiCellCount;
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
    // private readonly WorldBuffer<float> _elevation;
    // private readonly WorldBuffer<int> _landWaterMap;
    // private readonly WorldBuffer<Point> _voronoiCells;
    // private readonly WorldBuffer<bool> _voronoiCellTypes;
    // private readonly WorldBuffer<int> _voronoiCellIndex;
    // private readonly WorldBuffer<Point> _plateCells;
    // private readonly WorldBuffer<bool> _plateTypes;
    // private readonly WorldBuffer<int> _plateIndex;
    // private readonly WorldBuffer<Vector2> _plateMotions;
    // private readonly WorldBuffer<Point> _flowDirections;
    // private readonly WorldBuffer<Point> _windDirections;
    // private readonly WorldBuffer<byte> _windSpeeds;
    // private readonly WorldBuffer<float> _coastalSlopes;
    // private readonly WorldBuffer<float> _tectonicDelta;
    // private readonly WorldBuffer<float> _hotspotMap;
    // private readonly WorldBuffer<float> _noiseMap;
    // private readonly WorldBuffer<float> _temperature;
    // private readonly WorldBuffer<float> _temperatureAmplitude;
    // private readonly WorldBuffer<float> _humidity;
    // private readonly WorldBuffer<float> _distToWaterMap;
    // private readonly WorldBuffer<Biome> _biomes;

    // private readonly WorldBuffer<SurfaceFeature> _surfaceFeatures;

    // TODO: Merge surface features and rivers
    // private readonly WorldBuffer<bool> _riverMap;
    // private readonly WorldBuffer<int> _strahlerRiver;

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
        _voronoiCellCount = voronoiCellCount;
        _plateCount = plateCount;

        _elevationCfg = elevationCfg;
        _coastalCfg = coastalCfg;
        _volcanicCfg = volcanicCfg;
        _erosionCfg = erosionCfg;
        _climateCfg = climateCfg;
        _riverCfg = riverCfg;

        // init private fields
        // _elevation = new WorldBuffer<float>(worldWidth * worldHeight);
        // _landWaterMap = new WorldBuffer<int>(voronoiCellCount);
        // _voronoiCells = new WorldBuffer<Point>(voronoiCellCount);
        // _voronoiCellTypes = new WorldBuffer<bool>(voronoiCellCount);
        // _voronoiCellIndex = new WorldBuffer<int>(worldWidth * worldHeight);
        // _plateCells = new WorldBuffer<Point>(plateCount);
        // _plateTypes = new WorldBuffer<bool>(plateCount);
        // _plateIndex = new WorldBuffer<int>(voronoiCellCount);
        // _plateMotions = new WorldBuffer<Vector2>(voronoiCellCount);
        // _flowDirections = new WorldBuffer<Point>(worldWidth * worldHeight);
        // _windDirections = new WorldBuffer<Point>(worldWidth * worldHeight);
        // _windSpeeds = new WorldBuffer<byte>(worldWidth * worldHeight);
        // _coastalSlopes = new WorldBuffer<float>(worldWidth * worldHeight);
        // _tectonicDelta = new WorldBuffer<float>(worldWidth * worldHeight);
        // _hotspotMap = new WorldBuffer<float>(worldWidth * worldHeight);
        // _noiseMap = new WorldBuffer<float>(worldWidth * worldHeight);
        // _temperature = new WorldBuffer<float>(worldWidth * worldHeight);
        // _temperatureAmplitude = new WorldBuffer<float>(worldWidth * worldHeight);
        // _humidity = new WorldBuffer<float>(worldWidth * worldHeight);
        // _distToWaterMap = new WorldBuffer<float>(_worldWidth * _worldHeight);
        // _biomes = new WorldBuffer<Biome>(worldWidth * worldHeight);
        // _surfaceFeatures = new WorldBuffer<SurfaceFeature>(worldWidth * worldHeight);
        // _riverMap = new WorldBuffer<bool>(worldWidth * worldHeight);
        // _strahlerRiver = new WorldBuffer<int>(worldWidth * worldHeight);

        // init properties
        VoronoiCellCount = voronoiCellCount;
    }

    #endregion

    #region Public Members

    public WorldPackedChunk[] Generate()
    {
        var timer = new Stopwatch();
        // Stage 0: Preparation ~> Noise Map ///////////////////////////////////////////////////////
        timer.Start();
        var noiseMap = GenerateNoiseMap();
        timer.Stop();
        Console.WriteLine($"Generate Noise Map: {timer.ElapsedMilliseconds} ms");

        timer.Restart();
        // STAGE 1: Voronoi Cells and Land/Water distribution //////////////////////////////////////

        // Associate each grid cell to one of the voronoi cells.
        // For each voronoi land cell, apply perlin or simplex noise to generate height.
        var voronoiCells = new Point[VoronoiCellCount];
        var landWaterMap = new int[VoronoiCellCount];
        var voronoiCellTypes = InitializeVoronoiCells(voronoiCells, landWaterMap);
        timer.Stop();
        Console.WriteLine($"Generate Voronoi Cells: {timer.ElapsedMilliseconds} ms");

        timer.Restart();
        var (
                voronoiCellIndex,
                secondVoronoiIndex,
                voronoiDistToWinner,
                voronoiDistToSecond,
                elevations) =
            GenerateLandWaterDistribution(noiseMap, voronoiCells, landWaterMap);
        timer.Stop();
        Console.WriteLine($"Generate LandWater Distribution: {timer.ElapsedMilliseconds} ms");

        // Generate coastal slopes for each voronoi cell.
        // GenerateSlopedCoasts();

        // Stage 2: Plate Tectonics ////////////////////////////////////////////////////////////////

        timer.Restart();
        var (plateCells, plateTypes, plateIndex, plateMotions) =
            InitializePlateTectonics(noiseMap, voronoiCells, voronoiCellTypes);
        timer.Stop();
        Console.WriteLine($"Generate Plate Tectonics: {timer.ElapsedMilliseconds} ms");

        // Compute plate tectonics influence (mountains/trenches along plate boundaries).
        timer.Restart();
        var tectonicDelta = ComputePlateTectonicHeight(
            voronoiCellIndex,
            secondVoronoiIndex,
            voronoiCellTypes,
            voronoiDistToWinner,
            voronoiDistToSecond,
            plateCells,
            plateIndex,
            plateTypes,
            plateMotions);
        timer.Stop();
        Console.WriteLine($"Compute Plate Tectonic Height: {timer.ElapsedMilliseconds} ms");

        // Generate hotspots (mantle plumes creating volcanic islands/seamounts).
        timer.Restart();
        var hotspots = GenerateHotspots(noiseMap, voronoiCells, voronoiCellTypes, plateMotions);
        timer.Stop();
        Console.WriteLine($"Generate Hotspot Distribution: {timer.ElapsedMilliseconds} ms");

        // Apply all elevation changes from tectonics, hotspots etc.
        timer.Restart();
        ApplyTectonics(noiseMap, elevations, tectonicDelta, hotspots);
        timer.Stop();
        Console.WriteLine($"Apply Tectonic Delta: {timer.ElapsedMilliseconds} ms");

        // Stage 3: Climate ////////////////////////////////////////////////////////////////////////

        // Generate wind field now that elevation and noise are available. Wind
        // is used by the rainfall/shadow simulation and must be generated
        // before biome assignment.
        timer.Restart();
        var (windDirections, windSpeeds) = CalculateWindField(noiseMap, elevations);
        timer.Stop();
        Console.WriteLine($"Generate Wind Field: {timer.ElapsedMilliseconds} ms");

        // CalculateDistanceToWaterMap(); // TODO: This does not seem to be used.
        // Generate climate (temperature, humidity, biomes, seasonal effects)
        timer.Restart();
        var riverMap = new bool[_worldWidth * _worldHeight];
        var strahlerRiverMap = new byte[_worldWidth * _worldHeight];
        var (temperature, humidity, biomes) =
            GenerateClimate(noiseMap, elevations, windDirections, riverMap, strahlerRiverMap);
        timer.Stop();
        Console.WriteLine($"Generate Climate: {timer.ElapsedMilliseconds} ms");

        // Apply erosion to smooth terrain and create realistic features
        timer.Restart();
        ApplyErosion(elevations, humidity, hotspots);
        timer.Stop();
        Console.WriteLine($"Apply Erosion: {timer.ElapsedMilliseconds} ms");

        // Generate rivers based on rainfall and elevation (tunable via public properties)
        timer.Restart();
        var flowDirections = GenerateRivers(
            noiseMap,
            elevations,
            temperature,
            riverMap,
            strahlerRiverMap,
            windDirections);
        timer.Stop();
        Console.WriteLine($"Generate Rivers: {timer.ElapsedMilliseconds} ms");

        // Re-generate climate, now that we have rivers:
        timer.Restart();
        (temperature, humidity, biomes) =
            GenerateClimate(noiseMap, elevations, windDirections, riverMap, strahlerRiverMap);
        timer.Stop();
        Console.WriteLine($"Generate Climate: {timer.ElapsedMilliseconds} ms");

        // Stage 4: Surface features ///////////////////////////////////////////////////////////////

        // Apply mountain details (ridges, snow, glacier, lava)
        timer.Restart();
        var surfaceFeatures = ApplyMountainDetails(
            noiseMap,
            voronoiCellIndex,
            plateTypes,
            plateIndex,
            elevations,
            flowDirections,
            temperature,
            hotspots,
            riverMap
        );
        timer.Stop();
        Console.WriteLine($"Generate Mountain Surface Features: {timer.ElapsedMilliseconds} ms");

        // Apply coastal features (beach, cliff, fjord)
        timer.Restart();
        ApplyCoastalFeatures(elevations, riverMap, surfaceFeatures);
        timer.Stop();
        Console.WriteLine($"Generate Coastal Surface Features: {timer.ElapsedMilliseconds} ms");

        // Wind field was calculated earlier (before biome generation).

        Console.ReadKey();

        return ToPackedChunks(
            elevations,
            humidity,
            temperature,
            biomes,
            surfaceFeatures,
            flowDirections,
            windDirections,
            windSpeeds
        );
    }

    #endregion

    #region Noise Map

    /// <summary>
    ///     Generates a map of pseudo-random Perlin noise.
    /// </summary>
    private float[] GenerateNoiseMap()
    {
        var noise = new FastNoiseLite(_seed);
        noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        noise.SetFractalType(FastNoiseLite.FractalType.FBm);
        noise.SetFractalOctaves(4);
        noise.SetFractalLacunarity(3.0f);
        noise.SetFractalGain(0.5f);
        noise.SetFrequency(0.04f);

        var noiseMap = new float[_worldWidth * _worldHeight];
        // We calculate a radius that keeps the scale consistent.
        // A larger radius results in more "zoomed in" noise.
        var radius = _worldWidth / (2.0f * (float)Math.PI);

        for (var y = 0; y < _worldHeight; y++)
        {
            var rowOffset = y * _worldWidth;
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
                noiseMap[rowOffset + x] = (noiseVal + 1.0f) / 2.0f;
            }
        }

        return noiseMap;
    }

    #endregion

    #region World Base Structure

    /// <summary>
    ///     Initializes Voronoi cells of the world.
    /// </summary>
    /// <returns>Voronoi cells and their types, plate motions and land/water distribution.</returns>
    private bool[] InitializeVoronoiCells(
        Span<Point> voronoiCells,
        Span<int> landWaterMap
    )
    {
        // step 1: randomly sample <cellCount> coordinates of the grid as voronoi cell seeds
        for (var i = 0; i < VoronoiCellCount; i += 1)
            voronoiCells[i] = new Point(_rng.Next(_worldWidth), _rng.Next(_worldHeight));

        var voronoiCellTypes = GenerateVoronoiCellTypes();

        for (var i = 0; i < VoronoiCellCount; i += 1)
            // Lower oceanic plates to create deeper oceans
            landWaterMap[i] = voronoiCellTypes[i]
                ? _elevationCfg.LandElevationThreshold
                : _elevationCfg.LandElevationThreshold - 1;

        return voronoiCellTypes;
    }

    /// <summary>
    ///     Generates the type for each plate:
    ///     true == continental, false == oceanic.
    /// </summary>
    private bool[] GenerateVoronoiCellTypes()
    {
        var voronoiCellTypes = new bool[VoronoiCellCount];
        var land = 0f;
        var water = 0f;
        for (var i = 0; i < VoronoiCellCount; i += 1)
        {
            voronoiCellTypes[i] = _rng.NextDouble() < LandRatio;
            if (voronoiCellTypes[i])
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
        return voronoiCellTypes;
    }

    /// <summary>
    ///     Assigns an elevation and voronoi cell to each single cell on the map.
    /// </summary>
    private (
        int[] voronoiCellIndex,
        int[] secondVoronoiIndex,
        float[] voronoiDistToWinner,
        float[] voronoiDistToSecond,
        float[] elevations)
        GenerateLandWaterDistribution(
            ReadOnlyMemory<float> noiseMap,
            ReadOnlyMemory<Point> voronoiCells,
            ReadOnlyMemory<int> landWaterMap
        )
    {
        const int jiggle = 40;
        var voronoiCellIndex = new int[_worldWidth * _worldHeight];
        var secondVoronoiIndex = new int[_worldWidth * _worldHeight];
        var voronoiDistToWinner = new float[_worldWidth * _worldHeight];
        var voronoiDistToSecond = new float[_worldWidth * _worldHeight];
        var elevations = new float[_worldWidth * _worldHeight];

        Parallel.For(0, _worldHeight, y =>
        {
            // Thread-local Spans created from the captured Memory
            var noiseSpan = noiseMap.Span;
            var voronoiSpan = voronoiCells.Span;
            var landWaterSpan = landWaterMap.Span;
            var localLand = 0;
            var localWater = 0;
            var rowOffset = y * _worldWidth;
            for (var x = 0; x < _worldWidth; x += 1)
            {
                var idx = rowOffset + x;
                var jiggleNoise = noiseSpan[idx];
                var jiggledX = x + (0.5f - jiggleNoise) * jiggle;
                var jiggledY = y + (0.5f - jiggleNoise) * jiggle;

                var minDistSq = float.MaxValue;
                var secondMinDistSq = float.MaxValue;
                var winnerCell = 0;
                var secondWinnerCell = 0;
                for (var i = 0; i < VoronoiCellCount; i += 1)
                {
                    var vX = voronoiSpan[i].X;
                    var vY = voronoiSpan[i].Y;
                    var distSq = WorldMath.GetCylindricalDistanceSq(jiggledX, jiggledY, vX, vY);

                    if (distSq < minDistSq)
                    {
                        // The old closest becomes the new second-closest
                        secondMinDistSq = minDistSq;
                        secondWinnerCell = winnerCell;

                        // The new dist becomes the closest
                        minDistSq = distSq;
                        winnerCell = i;
                    }
                    else if (distSq < secondMinDistSq)
                    {
                        // If it's not closer than the first, it might be closer than the second
                        secondMinDistSq = distSq;
                        secondWinnerCell = i;
                    }
                }

                voronoiDistToWinner[idx] = MathF.Sqrt(minDistSq);
                voronoiDistToSecond[idx] = MathF.Sqrt(secondMinDistSq);
                voronoiCellIndex[idx] = winnerCell;
                secondVoronoiIndex[idx] = secondWinnerCell;

                elevations[idx] = landWaterSpan[winnerCell] >= _elevationCfg.LandElevationThreshold
                    ? _elevationCfg.LandElevationThreshold + MathF.Pow(noiseSpan[idx], 2.2f) *
                    (_elevationCfg.MaxElevation - _elevationCfg.LandElevationThreshold - 1)
                    : noiseSpan[idx] * (_elevationCfg.LandElevationThreshold - 1);

                if (elevations[idx] >= _elevationCfg.LandElevationThreshold)
                {
                    localLand++;
                }
                else
                {
                    localWater++;
                }
            }
        });

        return (voronoiCellIndex, secondVoronoiIndex, voronoiDistToWinner, voronoiDistToSecond,
            elevations);
    }

    #endregion

    #region Tectonics

    /// <summary>
    ///     Initializes plates and tectonic parameters.
    /// </summary>
    private (Point[], bool[], int[], Vector2[]) InitializePlateTectonics(
        Span<float> noiseMap, // Pass in your world noise map
        Span<Point> voronoiCells,
        Span<bool> voronoiCellTypes
    )
    {
        var plateCells = new Point[_plateCount];
        var plateTypes = new bool[_plateCount];
        var plateIndex = new int[_voronoiCellCount];

        // 1. SAFE SEED SELECTION (Preventing twin plates)
        var chosenSeeds = new HashSet<int>();
        for (var i = 0; i < _plateCount; i++)
        {
            int voronoiIndex;
            do
            {
                voronoiIndex = _rng.Next(_voronoiCellCount);
            } while (!chosenSeeds.Add(voronoiIndex)); // Ensure unique centers

            var voronoiCell = voronoiCells[voronoiIndex];
            plateCells[i] = new Point(voronoiCell.X, voronoiCell.Y);
            plateTypes[i] = voronoiCellTypes[voronoiIndex];
        }

        // TWEAKABLE: How violently the boundaries snake and interlock.
        // A value of 10-20% of your world width usually looks great.
        var warpStrength = _worldWidth * 0.15f;

        // 2. ASSIGN CELLS TO PLATES (With Domain Warping)
        for (var j = 0; j < _voronoiCellCount; j++)
        {
            var minDistSq = float.MaxValue;
            var winnerCell = 0;
            var (vX, vY) = voronoiCells[j];

            // Sample noise at the cell's center to get a warp vector.
            // We use the 1D noise map but offset the lookup to get an X and Y warp.
            var cellIdx = vY * _worldWidth + vX;

            // Pseudo-random offset for the Y noise so X and Y warp independently
            var offsetIdx = (cellIdx + (_worldWidth / 2)) % noiseMap.Length;

            // Normalize noise from [0, 1] to [-1, 1] and scale by warp strength
            var warpX = (noiseMap[cellIdx] - 0.5f) * 2.0f * warpStrength;
            var warpY = (noiseMap[offsetIdx] - 0.5f) * 2.0f * warpStrength;

            // Apply the warp to the cell's position
            var warpedVx = vX + warpX;
            var warpedVy = vY + warpY;

            for (var i = 0; i < _plateCount; i++)
            {
                var (pX, pY) = plateCells[i];

                // Calculate distance using the WARPED coordinates
                var distSq = WorldMath.GetCylindricalDistanceSq(warpedVx, warpedVy, pX, pY);

                if (distSq >= minDistSq) continue; // || voronoiCellTypes[j] != plateTypes[i]) continue;
                minDistSq = distSq;
                winnerCell = i;
            }

            plateIndex[j] = winnerCell;
        }

        // 3. Assign plate motions
        var plateMotions = GeneratePlateMotions();
        return (plateCells, plateTypes, plateIndex, plateMotions);
    }

    /// <summary>
    ///     Generates a motion vector for each plate.
    /// </summary>
    private Vector2[] GeneratePlateMotions()
    {
        var motions = new Vector2[_plateCount];
        for (var i = 0; i < _plateCount; i += 1)
        {
            var angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
            // TODO: Play around with the speed formula.
            var speed = (float)_rng.NextDouble() * 1.5f; // * 0.5 + 0.1);
            motions[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;
        }

        return motions;
    }

    /// <summary>
    ///     Generate elevation differences at tectonic plate boundaries,
    ///     e.g.: Mountains and seamounts at plate convergences and
    ///     trenches at plate divergences.
    ///     These changes will only affect world cells located at tectonic plate boundaries.
    /// </summary>
    private float[] ComputePlateTectonicHeight(
        Span<int> voronoiIndex,
        Span<int> secondVoronoiIndex,
        Span<bool> voronoiCellTypes,
        Span<float> dist1,
        Span<float> dist2,
        Span<Point> plateCells,
        Span<int> plateIndex,
        Span<bool> plateTypes,
        Span<Vector2> plateMotions
    )
    {
        var tectonicDelta = new float[_worldWidth * _worldHeight];
        var plateCount = plateMotions.Length;

        // TWEAKABLE: How many pixels wide are your mountain ranges?
        var rangeWidth = 11.0f;

        // Pre-calculate stress between every possible plate pair (O(P^2))
        // This avoids recalculating Dot products millions of times.
        var stressLookup = new float[plateCount * plateCount];
        for (var i = 0; i < plateCount; i++)
        {
            for (var j = 0; j < plateCount; j++)
            {
                if (i == j) continue;
                var relMotion = WorldMath.GetWrappedVector(plateMotions[j], plateMotions[i]);
                var dir = WorldMath.GetWrappedVector(plateCells[i], plateCells[j]);
                if (dir.LengthSquared() < 0.001f) continue;

                var normal = Vector2.Normalize(dir);
                var stress = Vector2.Dot(relMotion, normal);

                stressLookup[i * plateCount + j] = -stress;
            }
        }

        // The Pixel Loop
        for (var i = 0; i < _worldWidth * _worldHeight; i++)
        {
            var p1 = plateIndex[voronoiIndex[i]];
            var p2 = plateIndex[secondVoronoiIndex[i]];

            if (p1 == p2) continue; // Inside a plate, no tectonic stress

            // Calculate distance from the boundary line
            var deltaDist = dist2[i] - dist1[i];

            if (!(deltaDist < rangeWidth)) continue;
            // Normalize influence: 1.0 at the crack, 0.0 at the range edge
            var influence = 1.0f - (deltaDist / rangeWidth);

            // Smoothstep (Cubic) falloff for more natural mountain shapes
            // This prevents "sharp pyramid" mountains.
            influence = influence * influence * (3 - 2 * influence);

            var stress = stressLookup[p1 * plateCount + p2];

            // Apply your existing continental/oceanic multipliers here
            var multiplier = 1.0f;
            var isRecipientCont = voronoiCellTypes[voronoiIndex[i]];
            var isAggressorCont = voronoiCellTypes[secondVoronoiIndex[i]];

            if (stress < 0) // Convergence (Crashing)
            {
                if (isRecipientCont && !isAggressorCont)
                {
                    // I am a continent being hit by an ocean. 
                    // I crumple UP.
                    multiplier = 18f;
                }
                else if (!isRecipientCont && isAggressorCont)
                {
                    // I am an ocean hitting a continent. 
                    // I get dragged DOWN.
                    multiplier = -12f; // Negative creates the trench!
                }
                else if (!isRecipientCont && !isAggressorCont)
                {
                    // I am an ocean hitting an ocean. 
                    // Prevent hugh mountains in the middle of sea.
                    multiplier = 2f;
                }
                else if (isRecipientCont && isAggressorCont)
                {
                    // Two continents hitting each other (Himalayas).
                    // Both go UP.
                    multiplier = 25f;
                }
            }

            tectonicDelta[i] = stressLookup[p1 * plateCount + p2] * multiplier * influence;
        }

        return tectonicDelta;
    }

    /// <summary>
    ///     Assign hotspots to certain cells in the world.
    /// </summary>
    private float[] GenerateHotspots(
        Span<float> noiseMap,
        Span<Point> voronoiCells,
        Span<bool> voronoiCellTypes,
        Span<Vector2> plateMotions)
    {
        var hotspots = new float[_worldWidth * _worldHeight];
        var chainCount = _rng.Next(_volcanicCfg.MinIslandChains, _volcanicCfg.MaxIslandChains + 1);

        var oceanPlateIds = new List<int>();
        for (var i = 0; i < voronoiCellTypes.Length; i++)
            if (!voronoiCellTypes[i])
                oceanPlateIds.Add(i);

        // Track local min/max to avoid global state contention during the loops
        float localMinHeight = float.MaxValue;
        float localMaxHeight = float.MinValue;

        for (var chain = 0; chain < chainCount; chain++)
        {
            if (oceanPlateIds.Count == 0) continue;

            var oceanPlateId = oceanPlateIds[_rng.Next(oceanPlateIds.Count)];
            oceanPlateIds.Remove(oceanPlateId);
            var (startX, startY) = voronoiCells[oceanPlateId];

            var chainLength =
                _rng.Next(_volcanicCfg.MinChainLength, _volcanicCfg.MaxChainLength + 1);

            var chainDirection = plateMotions.Length > 0
                ? plateMotions[_rng.Next(plateMotions.Length)]
                : new Vector2((float)(_rng.NextDouble() - 0.5) * 2,
                    (float)(_rng.NextDouble() - 0.5) * 2);

            var length = chainDirection.Length();
            if (length > 0)
                chainDirection = (chainDirection / length) * (float)(_rng.NextDouble() * 2 + 1);

            for (var i = 0; i < chainLength; i++)
            {
                var offsetX = (int)(chainDirection.X * i * _volcanicCfg.ChainSpacing);
                var offsetY = (int)(chainDirection.Y * i * _volcanicCfg.ChainSpacing);
                var centerX = WorldMath.WrapX(startX + offsetX);
                var centerY = startY + offsetY;

                if (centerY < 0 || centerY >= _worldHeight) continue;

                var radius = _rng.Next(_volcanicCfg.MinHotspotRadius,
                    _volcanicCfg.MaxHotspotRadius + 1);
                var radiusSq = radius * radius; // Pre-calculate for fast distance check

                var strength = (float)(_rng.NextDouble() *
                                       (_volcanicCfg.MaxHotspotStrength -
                                        _volcanicCfg.MinHotspotStrength) +
                                       _volcanicCfg.MinHotspotStrength);

                // Bounding box: Y clamps, but X does NOT clamp so we can wrap it
                var minY = Math.Max(0, centerY - radius);
                var maxY = Math.Min(_worldHeight, centerY + radius);
                var minX = centerX - radius;
                var maxX = centerX + radius;

                var isAtoll = MathF.Abs(_worldHeight / 2f - centerY) < _worldHeight / 3f &&
                              _rng.NextDouble() < 0.5f;

                for (var y = minY; y < maxY; y++)
                {
                    var rowOffset = y * _worldWidth;
                    var dy = y - centerY;
                    var dySq = dy * dy;

                    for (var x = minX; x <= maxX; x++)
                    {
                        var dx = x - centerX;
                        var distSq = (dx * dx) + dySq;

                        // FAST REJECTION: Skip the expensive math if outside the circle
                        if (distSq > radiusSq) continue;

                        // Properly wrap the X coordinate for array lookup
                        var wrappedX = WorldMath.WrapX(x);
                        var idx = rowOffset + wrappedX;

                        var distance = MathF.Sqrt(distSq);
                        var normalizedDist = distance / radius;

                        var coneHeight = MathF.Exp(-normalizedDist * 3.0f) * strength;
                        var noise = noiseMap[idx];
                        coneHeight *= 0.7f + noise * 0.6f;

                        hotspots[idx] += coneHeight;

                        // ATOLL LOGIC: Carve the center AFTER adding height to prevent chain overlaps 
                        // filling the lagoon back in. We simulate a caldera collapse to "Sea Level - 1"
                        if (isAtoll)
                        {
                            var atollThreshold = _volcanicCfg.MaxHotspotStrength * 0.6f;
                            if (hotspots[idx] > atollThreshold)
                            {
                                // Flatten the peak to create a lagoon rim, then sink the center
                                var collapseDepth = hotspots[idx] - atollThreshold;
                                hotspots[idx] = atollThreshold - (collapseDepth * 0.5f);
                            }
                        }

                        localMinHeight = Math.Min(localMinHeight, hotspots[idx]);
                        localMaxHeight = Math.Max(localMaxHeight, hotspots[idx]);
                    }
                }
            }
        }

        // Apply local state to global state once at the end
        _minHotspotHeight = Math.Min(_minHotspotHeight, localMinHeight);
        _maxHotspotHeight = Math.Max(_maxHotspotHeight, localMaxHeight);

        return hotspots;
    }

    private void ApplyTectonics(
        Span<float> noiseField,
        Span<float> elevations,
        Span<float> tectonicDelta,
        Span<float> hotspots
    )
    {
        // If you aren't using slopes, we should define a default 'base' noise intensity 
        // so the world isn't perfectly smooth.
        const float baseNoiseIntensity = 0.05f;

        var landCount = 0;
        var totalCells = _worldWidth * _worldHeight;

        for (var i = 0; i < totalCells; i++)
        {
            var currentElev = elevations[i];
            var isAboveThreshold = currentElev >= _elevationCfg.LandElevationThreshold;

            // 1. Calculate Detail Noise
            // Since coastalSlopes is uninitialized, I'm using a fallback value.
            // If you ever implement GenerateSlopedCoasts(), replace 'baseNoiseIntensity' 
            // with (coastalSlopes[i] / _coastalCfg.MaxCoastalSlope).
            var upOrDownwards = isAboveThreshold ? 1.0f : -1.0f;
            var detailNoise = baseNoiseIntensity * noiseField[i] * upOrDownwards * currentElev;

            // 2. Combine Layers
            // Base + Detail + Tectonic Uplift + Volcanic Hotspot
            var finalElevation = currentElev + detailNoise + tectonicDelta[i] + hotspots[i];

            // 3. Clamp and Store
            // Ensure we don't exceed the atmosphere's ceiling.
            var clampedElev = Math.Min(_elevationCfg.MaxElevation, finalElevation);
            elevations[i] = clampedElev;

            // 4. Statistics
            if (clampedElev >= _elevationCfg.LandElevationThreshold)
            {
                landCount++;
            }
        }

        var ratio = (float)landCount / totalCells;
        Console.WriteLine($"Tectonics Applied: {ratio * 100:F2}% land");
    }

    #endregion

    #region Climate and Biomes

    /// <summary>
    /// Generates a wind direction field influenced by global bands (Hadley cells,
    /// trade winds, westerlies, polar easterlies), small-scale noise and elevation.
    /// Results are stored in `_windDirections` as discrete (-1,0,1) integer vectors.
    /// </summary>
    private (Point[], byte[]) CalculateWindField(Span<float> noiseMap, Span<float> elevations)
    {
        var windDirections = new Point[_worldWidth * _worldHeight];
        var windSpeeds = new byte[_worldWidth * _worldHeight];

        // Compute max elevation for normalization (avoid using _maxElevation which is set later)
        var maxElev = float.MinValue;
        var total = _worldWidth * _worldHeight;
        for (var i = 0; i < total; i++) maxElev = MathF.Max(maxElev, elevations[i]);
        if (maxElev <= 0) maxElev = 1f;

        var mid = _worldHeight / 2f;

        for (var y = 0; y < _worldHeight; y++)
        {
            var rowOffset = y * _worldWidth;
            // Absolute latitude 0 at equator -> 1 at poles
            var latitudeAbs = MathF.Abs(y - mid) / mid;

            // Band selection: trade winds (near equator) and westerlies (mid-latitudes)
            // and polar easterlies (near poles). This mirrors common atmospheric cells.
            var bandDx = latitudeAbs is < 0.33f or >= 0.66f ? -1f : 1f;

            // Hemispheric sign: north (y < mid) => -1, south (y > mid) => +1, equator => 0
            var hemisphere = MathF.Sign(y - mid);

            // Meridional component: trades blow toward the equator, westerlies toward poles
            var bandDy = bandDx < 0 ? -hemisphere : hemisphere;

            for (var x = 0; x < _worldWidth; x++)
            {
                var idx = rowOffset + x;

                // Local noise to add small-scale variation
                var noiseX = (noiseMap[idx] * 2f - 1f) * 0.25f; // approx -0.25..0.25
                // Read from an arbitrary large offset (e.g., halfway across the map) for Y noise
                var offsetIdx = (idx + (_worldWidth * _worldHeight / 2)) %
                                (_worldWidth * _worldHeight);
                var noiseY = (noiseMap[offsetIdx] * 2f - 1f) * 0.25f;

                // Base floating vector
                var fx = bandDx + noiseX;
                var fy = bandDy + noiseY * 0.5f;

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
                if (finalDx != 0 || finalDy != 0)
                {
                    var targetX = WorldMath.WrapX(x + finalDx);
                    // Clamp Y to prevent trying to read outside the array at the poles
                    var targetY = Math.Clamp(y + finalDy, 0, _worldHeight - 1);
                    var targetIdx = targetY * _worldWidth + targetX;

                    // Positive slope means uphill. We don't care about downhill (negative).
                    var directionalSlope = elevations[targetIdx] - elevations[idx];

                    // If the cell the wind is moving into is drastically higher, block it entirely.
                    if (directionalSlope >
                        (maxElev * 0.2f)) // 20% of highest peak in one step is a cliff
                    {
                        finalDx = 0;
                        finalDy = 0;
                    }
                }

                // Store discrete wind direction
                windDirections[idx] = new Point(finalDx, finalDy);

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

        return (windDirections, windSpeeds);
    }

    /// <summary>
    ///     Generates world maps for various climate features.
    /// </summary>
    private (float[], float[], Biome[]) GenerateClimate(
        Span<float> noiseMap,
        Span<float> elevations,
        Span<Point> windDirections,
        Span<bool> riverMap,
        Span<byte> strahlerRiver)
    {
        var temperature = new float[_worldWidth * _worldHeight];
        var humidity = new float[_worldWidth * _worldHeight];
        var biomes = new Biome[_worldWidth * _worldHeight];

        // STEP 1: CALCULATE TEMPERATURE FIRST
        // Everything else (Rainfall and Humidity) depends on temperature.
        for (var y = 0; y < _worldHeight; y++)
        {
            var rowOffset = y * _worldWidth;
            // Smoother latitude warping using the noise map
            var latWarp = (noiseMap[rowOffset] * 2 - 1) * 4.0f;
            var latitude = MathF.Abs(y + latWarp - _worldHeight / 2f) / (_worldHeight / 2f);
            var baseTemp = _climateCfg.BaseTempMax -
                           (_climateCfg.BaseTempMax - _climateCfg.BaseTempMin) *
                           MathF.Pow(latitude, 1.2f);

            for (var x = 0; x < _worldWidth; x++)
            {
                var idx = rowOffset + x;
                var elevationFactor = elevations[idx] / _elevationCfg.MaxElevation;

                // CORRECTED LAPSE RATE: Temperature drops as elevation rises
                // We subtract the elevation factor here.
                temperature[idx] = baseTemp - (elevationFactor * 25.0f);
            }
        }

        // STEP 2: GENERATE RAINFALL (Now with correct temperatures!)
        var rainfallMap =
            CalculateUnifiedRainfall(noiseMap, elevations, temperature, riverMap, windDirections);

        // STEP 3: CALCULATE HUMIDITY AND BIOMES
        for (var idx = 0; idx < temperature.Length; idx++)
        {
            var absoluteMoisture = rainfallMap[idx];

            // STABILIZED HUMIDITY: 
            // Instead of raw division, we use a softer saturation curve.
            // As air gets colder, it needs less moisture to reach 100% humidity.
            var tempFactor = Math.Clamp((temperature[idx] + 20) / 50f, 0.1f, 1.5f);
            humidity[idx] = Math.Clamp(absoluteMoisture / tempFactor, 0.0f, 1.0f);

            // STEP 4: DETERMINE BIOME
            biomes[idx] = DetermineBiome(
                temperature[idx],
                humidity[idx],
                elevations[idx],
                elevations[idx] < _elevationCfg.LandElevationThreshold,
                riverMap[idx],
                strahlerRiver[idx]);
        }

        return (temperature, humidity, biomes);
    }

    private float[] CalculateUnifiedRainfall(
        Span<float> noiseMap,
        Span<float> elevations,
        Span<float> temperatures,
        Span<bool> riverMap,
        Span<Point> windDirections)
    {
        var rainfall = new float[_worldWidth * _worldHeight];

        // Tuning Constants
        const float moistureRechargeRate = 0.09f;
        const float rainDropFactor = 0.06f;
        const float landDecayRate = 0.995f; // Slightly higher to prevent mid-continent "dead zones"

        for (var y = 0; y < _worldHeight; y++)
        {
            var rowOffset = y * _worldWidth;

            // 1. Determine Global Wind Row Direction
            var sumX = 0;
            for (var sx = 0; sx < _worldWidth; sx++)
                sumX += windDirections[rowOffset + sx].X;
            var windDir = sumX >= 0 ? 1 : -1;

            // 2. Initial Cloud State
            var cloudMoisture = _riverCfg.RainfallLandBase;

            // 3. Two-Pass Sweep (for world wrapping)
            for (var step = 0; step < _worldWidth * 2; step++)
            {
                var windX = WorldMath.WrapX(windDir == 1 ? step : -step);
                var idx = rowOffset + windX;
                var elev = elevations[idx];
                var isSecondPass = step >= _worldWidth;

                // Temperature factors
                var tempAtTile = temperatures[idx];
                var poleFactor =
                    tempAtTile switch
                    {
                        < -20 => 0.0f,
                        < -15 => 0.6f,
                        < -10 => 0.8f,
                        < -5 => 0.9f,
                        < 0 => 0.95f,
                        _ => 1.0f
                    };
                var evapPower = Math.Clamp((tempAtTile + 50) / 80f, 0.2f, 1.5f);

                if (elev < _elevationCfg.LandElevationThreshold || riverMap[idx])
                {
                    // OCEAN: Recharge
                    cloudMoisture = (MathF.Min(_riverCfg.RainfallOceanBase,
                        cloudMoisture + (moistureRechargeRate * evapPower))) * poleFactor;

                    if (isSecondPass)
                        rainfall[idx] = _riverCfg.RainfallOceanBase * poleFactor;
                }
                else
                {
                    // LAND: Discharge
                    var windVec = windDirections[idx];
                    var dx = windVec.X != 0 ? windVec.X : windDir;
                    var prevX = WorldMath.WrapX(windX - dx);
                    var prevY = Math.Clamp(y - windVec.Y, 0, _worldHeight - 1);

                    var lift = elev - elevations[prevY * _worldWidth + prevX];
                    float rainDropped = 0;

                    if (lift > 0)
                    {
                        rainDropped = MathF.Min(cloudMoisture,
                            lift * cloudMoisture * rainDropFactor);
                        cloudMoisture -= rainDropped;
                    }

                    if (isSecondPass)
                    {
                        // Apply Rain Shadow: Air with low moisture produces less ambient rain
                        // We blend 20% base rain with 80% moisture-dependent rain
                        var shadowMultiplier = 0.2f + (cloudMoisture * 0.8f);

                        var finalRain = (cloudMoisture * 0.15f * shadowMultiplier) +
                                        (rainDropped * 3.0f * noiseMap[idx] * poleFactor);

                        // Add local humidity from rivers
                        if (riverMap[idx]) finalRain += (_riverCfg.RainfallOceanBase * 0.25f);

                        // RECORD: Use += to allow for the 'Bleed' from neighbors
                        rainfall[idx] += finalRain;

                        // BLEED: Break horizontal lines
                        if (y > 0 && y < _worldHeight - 1)
                        {
                            var bleed = finalRain * 0.18f;
                            rainfall[(y - 1) * _worldWidth + windX] += bleed;
                            rainfall[(y + 1) * _worldWidth + windX] += bleed;
                        }
                    }

                    // Air naturally dries out as it moves over land
                    cloudMoisture *= landDecayRate;
                }
            }
        }

        return rainfall;
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
            if (temp < -15)
            {
                return Biome.PackIce;
            }
            else if (elevation < _elevationCfg.HighSeaThreshold)
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
                return temp < 25f ? Biome.Shallows : Biome.Reef;
            }

        if (isRiver)
        {
            if (temp < -8f) return Biome.IceCap;
            return strahlerOrder switch
            {
                <= 2 => Biome.Creek,
                <= 4 => Biome.MinorRiver,
                _ => Biome.MajorRiver
            };
        }


        // High Altitude "Dead Zone" (Above the Tree Line)
        if (elevation >= _elevationCfg.HighMountainThreshold)
            return temp < 0f ? Biome.Glacier : Biome.RockPeak;

        switch (temp)
        {
            // 3. Extreme Cold (Polar / Arctic)
            // High humidity in extreme cold leads to permanent ice sheets/glaciers.
            // Low humidity leads to barren, frozen gravel/dust plains.
            case < -10f when relHumidity < 0.3f:
                return Biome.PolarDesert;
            case < -10f when relHumidity < 0.6f:
                return Biome.IceCap;
            case < -10f when relHumidity >= 0.6f:
                return Biome.Glacier;
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
    private void ApplyErosion(
        Span<float> elevations,
        Span<float> humidity,
        Span<float> hotspots)
    {
        (int dx, int dy)[] vonNeumanDirections = [(0, -1), (1, 0), (0, 1), (-1, 0)];
        (int dx, int dy)[] directions =
            [(0, -1), (1, 0), (0, 1), (-1, 0), (1, -1), (1, 1), (-1, 1), (-1, -1)];

        // 1. Allocate arrays ONCE outside the loop (Double Buffering)
        var terrain = new float[_worldWidth * _worldHeight];
        var nextTerrain = new float[_worldWidth * _worldHeight];

        var water = new float[_worldWidth * _worldHeight];
        var nextWater = new float[_worldWidth * _worldHeight];

        var sediment = new float[_worldWidth * _worldHeight];
        var nextSediment = new float[_worldWidth * _worldHeight];

        // Initialize terrain
        elevations.CopyTo(terrain);

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
                var rowOffset = y * _worldWidth;
                for (var x = 0; x < _worldWidth; x++)
                {
                    var idx = rowOffset + x;
                    var currentWater = water[idx];

                    if (currentWater <= 0) continue;

                    // // The Ocean Sink
                    // // If this cell is underwater, the ocean absorbs the water and dissolves the sediment.
                    // // (Assuming you use LandElevationThreshold to define sea level like in your FillDepressions method)
                    if (terrain[idx] < _elevationCfg.LandElevationThreshold)
                    {
                        nextWater[idx] = 0f;
                        nextSediment[idx] = 0f;
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
                    var currentSediment = sediment[idx];

                    if (currentSediment > capacity)
                    {
                        // Deposit
                        var depositAmount =
                            (currentSediment - capacity) * _erosionCfg.DepositionRate;
                        nextTerrain[idx] += depositAmount;
                        nextSediment[idx] -= depositAmount;
                    }
                    else
                    {
                        // Erode
                        var erodeAmount = (capacity - currentSediment) *
                                          _erosionCfg.HydraulicErosionRate;

                        // Pipeline reuse: Hotspots resist erosion
                        if (hotspots[idx] > 0.1f) erodeAmount *= _volcanicCfg.VolcanicResistance;

                        erodeAmount = MathF.Min(erodeAmount, terrain[idx] * 0.1f);
                        nextTerrain[idx] -= erodeAmount;
                        nextSediment[idx] += erodeAmount;
                    }

                    // Outflow routing
                    var totalOutflow = 0f;

                    foreach (var (dx, dy) in vonNeumanDirections)
                    {
                        var nx = WorldMath.WrapX(x + dx);
                        var ny = y + dy;

                        if (ny < 0 || ny >= _worldHeight) continue;

                        var neighborIndex = ny * _worldWidth + nx;
                        var neighborSlope = terrain[idx] - terrain[neighborIndex];

                        if (neighborSlope <= 0) continue;

                        var outflow = currentWater * neighborSlope *
                                      _erosionCfg.HydraulicErosionRate;

                        // Distribute to next state
                        nextWater[neighborIndex] += outflow * 0.25f;
                        nextSediment[neighborIndex] += currentSediment * outflow /
                            MathF.Max(currentWater, _erosionCfg.WaterViscosity) * 0.25f;
                        totalOutflow += outflow;
                    }

                    nextWater[idx] -= totalOutflow;
                    nextSediment[idx] -= currentSediment * totalOutflow /
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
                var rowOffset = y * _worldWidth;
                for (var x = 0; x < _worldWidth; x++)
                {
                    var idx = rowOffset + x;
                    var currentHeight = terrain[idx]; // READ from static state

                    // Ignore water terrain.
                    if (currentHeight < _elevationCfg.LandElevationThreshold) continue;

                    var lowestNeighborHeight = currentHeight;
                    var lowestIndex = idx;

                    // 8-way check with wrapping
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
                    nextTerrain[idx] -= slideAmount;
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
            elevations[i] = terrain[i];
    }

    #endregion

    #region Rivers

    // TODO: Check whether to let this be influenced by biome, temperature or humidity as well.

    /// <summary>
    ///     Generates rivers on the map.
    /// </summary>
    private Point[] GenerateRivers(
        Span<float> noiseMap,
        Span<float> elevations,
        Span<float> temperature,
        Span<bool> riverMap,
        Span<byte> strahlerRiverMap,
        Span<Point> windDirections
    )
    {
        // Step 0: Fix local minima so water doesn't get stuck
        FillDepressions(elevations);

        // Step 1: Generate rainfall map
        var rainfall =
            CalculateUnifiedRainfall(noiseMap, elevations, temperature, riverMap, windDirections);

        // Step 2: Calculate flow directions (steepest downhill neighbor)
        var flowDirections = CalculateFlowDirections(elevations);

        // Step 3: Accumulate flow from upstream cells
        var flowAccumulation = AccumulateFlow(elevations, rainfall, flowDirections);
        CalculateStrahlerOrder(strahlerRiverMap, flowDirections, flowAccumulation);

        // Step 4: Carve rivers where flow accumulation is high enough
        CarveRivers(elevations, flowAccumulation, riverMap, strahlerRiverMap);
        return flowDirections;
    }

    /// <summary>
    ///     Fills local minima (pits) in the elevation data so that water can always flow to the ocean.
    ///     Implements a simplified Planchon-Darboux algorithm.
    /// </summary>
    private void FillDepressions(Span<float> elevations)
    {
        var totalCells = _worldWidth * _worldHeight;

        // We only need to know if we've processed a cell to prevent infinite loops
        var visited = new bool[totalCells];
        Array.Clear(visited, 0, totalCells); // Always clear rented arrays!

        // The queue stores the cell index, sorted by its water level (priority)
        var pq = new PriorityQueue<int, float>(totalCells / 10); // Rough initial capacity

        // Pre-allocate the directions array ONCE, outside the loops
        ReadOnlySpan<(int dx, int dy)> directions =
        [
            (0, -1), (1, 0), (0, 1), (-1, 0)
        ];

        // Step 1: Enqueue all outlets (oceans and map edges)
        for (var y = 0; y < _worldHeight; y++)
        {
            var rowOffset = y * _worldWidth;
            for (var x = 0; x < _worldWidth; x++)
            {
                var idx = rowOffset + x;
                var elev = elevations[idx];

                if ((elev >= _elevationCfg.LandElevationThreshold) && y != 0 &&
                    y != _worldHeight - 1) continue;
                pq.Enqueue(idx, elev);
                visited[idx] = true;
            }
        }

        // Step 2: Flood inward from the lowest points
        while (pq.Count > 0)
        {
            // Get the lowest current tile
            pq.TryDequeue(out var currIdx, out var currElev);

            var cx = currIdx % _worldWidth;
            var cy = currIdx / _worldWidth;

            foreach (var (dx, dy) in directions)
            {
                var nx = WorldMath.WrapX(cx + dx);
                var ny = cy + dy;

                // Bounds check Y
                if (ny < 0 || ny >= _worldHeight) continue;

                var nIdx = ny * _worldWidth + nx;

                // If we've already flooded this cell, skip it
                if (visited[nIdx]) continue;

                visited[nIdx] = true;

                // Water flows downhill. If this neighbor is lower than the current water 
                // level, we must fill it up to the current level + a tiny slope.
                var neighborElev = elevations[nIdx];
                var newWaterLevel = MathF.Max(neighborElev, currElev + 0.001f);

                // Directly modify the span. No second array needed!
                elevations[nIdx] = newWaterLevel;

                // Push this newly processed tile onto the queue to check ITS neighbors
                pq.Enqueue(nIdx, newWaterLevel);
            }
        }
    }

    /// <summary>
    ///     Calculates a map of hypothetical water flow directions for each cell.
    /// </summary>
    /// <returns>A grid of flow vectors per cell.</returns>
    private Point[] CalculateFlowDirections(Span<float> elevations)
    {
        var flowDirections = new Point[_worldWidth * _worldHeight];
        Point[] directions = [new(0, -1), new(1, 0), new(0, 1), new(-1, 0)];

        for (var y = 0; y < _worldHeight; y++)
        {
            var rowOffset = y * _worldWidth;
            for (var x = 0; x < _worldWidth; x++)
            {
                var currentElevation = elevations[rowOffset + x];
                var steepestDrop = 0f;
                var bestDirection = new Point(0, 0);

                // Check 4 neighbors
                foreach (var (dx, dy) in directions)
                {
                    if (dx == 0 && dy == 0) continue;

                    var nx = WorldMath.WrapX(x + dx);
                    var ny = y + dy;

                    if (ny < 0 || ny >= _worldHeight) continue;

                    var idx = ny * _worldWidth + nx;
                    var neighborElevation = elevations[idx];
                    var drop = currentElevation - neighborElevation;
                    // if (dx != 0 && dy != 0) 
                    //     drop /= 1.4142135f; // Normalizes diagonal drop rate

                    if (drop <= steepestDrop) continue;

                    steepestDrop = drop;
                    bestDirection.X = dx;
                    bestDirection.Y = dy;
                }

                flowDirections[rowOffset + x] = bestDirection;
            }
        }

        return flowDirections;
    }

    /// <summary>
    ///     Simulate the flow of water down the elevations and keep track of where it ends up.
    /// </summary>
    /// <param name="flowDirections">Direction of flow for each cell.</param>
    /// <param name="rainfall">Mapping of rainfall amount per cell.</param>
    /// <returns>Map of accumulated flow for each cell.</returns>
    private float[] AccumulateFlow(
        Span<float> elevations,
        Span<float> rainfall,
        Span<Point> flowDirections)
    {
        var flowAccumulation = new float[_worldWidth * _worldHeight];
        var inDegree = new int[_worldWidth * _worldHeight];

        // Step 1: Initialize rainfall and calculate In-Degrees
        for (var y = 0; y < _worldHeight; y++)
        {
            var rowOffset = y * _worldWidth;
            for (var x = 0; x < _worldWidth; x++)
            {
                var idx = rowOffset + x;
                flowAccumulation[idx] = rainfall[idx];

                var (dx, dy) = flowDirections[idx];
                if (dx == 0 && dy == 0) continue;

                var nx = WorldMath.WrapX(x + dx);
                var ny = y + dy;

                if (ny >= 0 && ny < _worldHeight)
                    // This neighbor is receiving flow from (x, y)
                    inDegree[ny * _worldWidth + nx]++;
            }
        }

        // Step 2: Queue all cells that have NO incoming water (Sources / Ridge lines)
        var queue = new Queue<Point>();
        for (var y = 0; y < _worldHeight; y++)
        {
            var rowOffset = y * _worldWidth;
            for (var x = 0; x < _worldWidth; x++)
                if (inDegree[rowOffset + x] == 0)
                    queue.Enqueue(new Point(x, y));
        }

        // Step 3: Process the queue
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            var idx = y * _worldWidth + x;
            var (dx, dy) = flowDirections[idx];

            if (dx == 0 && dy == 0) continue;

            var nx = WorldMath.WrapX(x + dx);
            var ny = y + dy;

            if (ny < 0 || ny >= _worldHeight) continue;

            var currentVolume = flowAccumulation[y * _worldWidth + x];
            var waterToPass = 0f;

            // --- The Ocean Sink ---
            // If the current cell is in the ocean, it absorbs the water. 
            // We do NOT pass water to the next ocean cell.
            var isOcean = elevations[idx] < _elevationCfg.LandElevationThreshold;
            if (!isOcean)
            {
                // Ground Seepage (Absolute loss per tile)
                // Keep this small (e.g., 0.05f) so it only kills tiny streams, not main rivers.
                const float groundSeepage = 0.05f;

                // Surface Evaporation (Scales with the square root of volume)
                var aridity = 1.0f - rainfall[idx];
                const float evaporationFactor = 0.05f; // Tweak this based on map size

                // A river of volume 100 loses 10 * 0.05 = 0.5 volume.
                // A river of volume 10,000 loses 100 * 0.05 = 5.0 volume. (Much more survivable!)
                var evaporationLoss = MathF.Sqrt(currentVolume) * aridity * evaporationFactor;

                // Calculate remaining water
                waterToPass = currentVolume - groundSeepage - evaporationLoss;
            }


            var nIdx = ny * _worldWidth + nx;
            // Ensure we don't pass negative water (ocean cells pass 0).
            if (waterToPass > 0) flowAccumulation[nIdx] += waterToPass;

            // Mark that one upstream dependency is resolved.
            // We MUST still decrement the neighbor's inDegree even if we pass 0 water,
            // otherwise the topological sort will freeze and skip the rest of the map.
            inDegree[nIdx]--;

            // If all upstream dependencies are resolved, this cell is ready to flow
            if (inDegree[nIdx] == 0) queue.Enqueue(new Point(nx, ny));
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
    private void CalculateStrahlerOrder(
        Span<byte> strahlerRiverMap,
        Span<Point> flowDirections,
        Span<float> flowAccumulation)
    {
        var inDegree = new byte[_worldWidth * _worldHeight];
        var maxIncomingOrder = new byte[_worldWidth * _worldHeight];
        var countOfMaxOrder = new byte[_worldWidth * _worldHeight];

        // 1. Minimum volume required to even be considered a "stream"
        const float minVolumeThreshold = 0.51f;

        // Calculate in-degrees
        for (var y = 0; y < _worldHeight; y++)
        {
            var rowOffset = y * _worldWidth;
            for (var x = 0; x < _worldWidth; x++)
            {
                // Only count in-degree if the upstream cell actually has water!
                if (flowAccumulation[rowOffset + x] < minVolumeThreshold) continue;

                var (dx, dy) = flowDirections[rowOffset + x];
                if (dx == 0 && dy == 0) continue;
                var nx = WorldMath.WrapX(x + dx);
                var ny = y + dy;
                if (ny >= 0 && ny < _worldHeight) inDegree[ny * _worldWidth + nx]++;
            }
        }

        // 2. Queue the sources
        var queue = new Queue<Point>();
        for (var y = 0; y < _worldHeight; y++)
        {
            var rowOffset = y * _worldWidth;
            for (var x = 0; x < _worldWidth; x++)
            {
                var idx = rowOffset + x;
                // A source is a cell with water but no water flowing into it
                if (inDegree[idx] != 0 || !(flowAccumulation[idx] >= minVolumeThreshold))
                    continue;
                strahlerRiverMap[idx] = 1;
                queue.Enqueue(new Point(x, y));
            }
        }

        var maxOrder = float.MinValue;
        // 3. Process topologically
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            var idx = y * _worldWidth + x;
            var currentOrder = strahlerRiverMap[idx];

            var (dx, dy) = flowDirections[idx];
            if (dx == 0 && dy == 0)
            {
                continue;
            }

            var nx = WorldMath.WrapX(x + dx);
            var ny = y + dy;
            if (ny < 0 || ny >= _worldHeight) continue;
            var nIdx = ny * _worldWidth + nx;

            // Update the neighbor's knowledge of its incoming tributaries
            if (currentOrder > maxIncomingOrder[nIdx])
            {
                maxIncomingOrder[nIdx] = currentOrder;
                countOfMaxOrder[nIdx] = 1;
            }
            else if (currentOrder == maxIncomingOrder[nIdx])
            {
                countOfMaxOrder[nIdx]++;
            }

            inDegree[nIdx]--;
            if (inDegree[nIdx] != 0) continue;
            // RESOLVE ORDER: If two or more tributaries of the same MAX order meet, level up.
            // Otherwise, inherit the max incoming order.
            strahlerRiverMap[ny * _worldWidth + nx] = (byte)(countOfMaxOrder[nIdx] >= 2
                ? maxIncomingOrder[nIdx] + 1
                : maxIncomingOrder[nIdx]);

            maxOrder = MathF.Max(strahlerRiverMap[ny * _worldWidth + nx], maxOrder);

            queue.Enqueue(new Point(nx, ny));
        }
    }

    /// <summary>
    ///     Create a boolean river map of the world where true -> belongs to a river, false otherwise.
    /// </summary>
    /// <param name="flowAccumulation">Accumulated flow for each cell.</param>
    private void CarveRivers(
        Span<float> elevations,
        Span<float> flowAccumulation,
        Span<bool> riverMap,
        Span<byte> strahlerRiver)
    {
        riverMap.Clear();

        // Visual and Physical Thresholds
        const int minOrderToVisualise = 2;
        const int minOrderToCarve = 2;
        const float MinVolumeToCarve = 10.5f;

        // A single pass is all we need. Flow accumulation already guarantees continuity.
        for (var y = 0; y < _worldHeight; y++)
        {
            var rowOffset = y * _worldWidth;
            for (var x = 0; x < _worldWidth; x++)
            {
                var index = rowOffset + x;
                var cellElevation = elevations[index];

                // 1. Skip immediately if we are already in the ocean
                if (cellElevation < _elevationCfg.LandElevationThreshold)
                    continue;

                var order = strahlerRiver[index];
                var volume = flowAccumulation[index];

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

                elevations[index] = MathF.Max(minAllowedElevation, carvedElevation);
            }
        }
    }

    #endregion

    #region Mountain Features

    /// <summary>
    ///     Applies surface features to mountains, snow, glaciers, rivers and lava.
    /// </summary>
    private SurfaceFeature[] ApplyMountainDetails(
        Span<float> noiseField,
        Span<int> voronoiIndex,
        Span<bool> plateTypes,
        Span<int> plateIndex,
        Span<float> elevations,
        Span<Point> flowDirections,
        Span<float> temperatures,
        Span<float> hotspotMap,
        Span<bool> riverMap
    )
    {
        var surfaceFeatures = new SurfaceFeature[_worldWidth * _worldHeight];

        for (var i = 0; i < _worldWidth * _worldHeight; i++)
        {
            var elevation = elevations[i];
            var temperature = temperatures[i];

            surfaceFeatures[i] = SurfaceFeature.None;
            switch (temperature)
            {
                // 1. Check for Snow (Slightly warmer than glaciers, or high mountains)
                case < 0.25f when elevation >= _elevationCfg.LandElevationThreshold:
                    surfaceFeatures[i] = SurfaceFeature.Snow;
                    continue;
            }

            // 3. Check for Mountains (Purely geographical)
            if (elevation >= _elevationCfg.HighMountainThreshold)
            {
                surfaceFeatures[i] = SurfaceFeature.Mountain;
                continue;
            }

            if (hotspotMap[i] > _volcanicCfg.LavaHotspotThreshold &&
                elevation >= _elevationCfg.LandElevationThreshold)
                surfaceFeatures[i] = SurfaceFeature.Lava;
        }

        // Add volcanic details around hotspots
        var hotspotCenters = FindHotspotCenters(hotspotMap);
        foreach (var (centerX, centerY, strength) in hotspotCenters)
            AddVolcanicDetails(
                centerX,
                centerY,
                strength,
                noiseField,
                elevations,
                flowDirections,
                riverMap,
                surfaceFeatures);


        // Plate boundary mountain ridges: continental collisions
        var neighbors = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        for (var y = 0; y < _worldHeight; y++)
        {
            var rowOffset = y * _worldWidth;
            for (var x = 0; x < _worldWidth; x++)
            {
                var idx = rowOffset + x;
                var currentPlate = plateIndex[voronoiIndex[idx]];
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
                    if (elevations[idx] < _elevationCfg.LandElevationThreshold)
                        continue;
                    // Skip tiles with features already defined on them.
                    if (surfaceFeatures[idx] != SurfaceFeature.None) continue;

                    surfaceFeatures[idx] = SurfaceFeature.Mountain;
                }
            }
        }

        return surfaceFeatures;
    }

    /// <summary>
    ///     Finds center points of strong hot spots.
    /// </summary>
    /// <returns>List of hotspot centers</returns>
    private List<(int x, int y, float strength)> FindHotspotCenters(Span<float> hotspotMap)
    {
        var centers = new List<(int, int, float)>();
        var visited = new bool[_worldWidth * _worldHeight];

        for (var y = 0; y < _worldHeight; y++)
        {
            var rowOffset = y * _worldWidth;
            for (var x = 0; x < _worldWidth; x++)
            {
                var idx = rowOffset + x;
                if (visited[idx] ||
                    hotspotMap[idx] < _volcanicCfg.HotspotMinStrength) continue;

                // Find local maximum
                var maxStrength = hotspotMap[idx];
                var maxX = x;
                var maxY = y;

                // Search in a small radius for the actual peak
                for (var dy = -3; dy <= 3; dy++)
                {
                    var ny = y + dy;
                    var dRowOffset = ny * _worldWidth;
                    for (var dx = -3; dx <= 3; dx++)
                    {
                        var nx = WorldMath.WrapX(x + dx);
                        if (ny < 0 || ny >= _worldHeight) continue;
                        if (hotspotMap[dRowOffset + nx] <= maxStrength) continue;

                        maxStrength = hotspotMap[ny * _worldWidth + nx];
                        maxX = nx;
                        maxY = ny;
                    }
                }

                // Mark area around peak as visited
                for (var dy = -5; dy <= 5; dy++)
                    for (var dx = -5; dx <= 5; dx++)
                    {
                        var nx = WorldMath.WrapX(maxX + dx);
                        var ny = maxY + dy;
                        if (ny >= 0 && ny < _worldHeight)
                            visited[ny * _worldWidth + nx] = true;
                    }

                centers.Add((maxX, maxY, maxStrength));
            }
        }

        return centers;
    }

    private void AddVolcanicDetails(
     int centerX,
     int centerY,
     float strength,
     Span<float> noiseField,// Added to break up perfect circles
     Span<float> elevations,
     Span<Point> flowDirections,
     Span<bool> riverMap,
     Span<SurfaceFeature> surfaceFeatures
 )
    {
        var radius = Math.Max(3, (int)(strength * 10));

        var volcanoType = strength switch
        {
            > 0.9f => SurfaceFeature.Stratovolcano,
            > 0.6f => SurfaceFeature.Shield,
            _ => SurfaceFeature.Cinder
        };

        // 1. GENERATE THE MACRO STRUCTURE (With Noise Warp)
        // We add a little noise to the distance calculation so the rings are jagged
        for (var y = Math.Max(0, centerY - radius); y < Math.Min(_worldHeight, centerY + radius); y++)
        {
            var rowOffset = y * _worldWidth;
            for (var x = centerX - radius; x < centerX + radius; x++)
            {
                var wrappedX = WorldMath.WrapX(x);
                var idx = rowOffset + wrappedX;
                var distance = WorldMath.GetCylindricalDistance(wrappedX, y, centerX, centerY);

                if (distance > radius) continue;

                // Inject noise to make the feature rings irregular. 
                // Normalize noise to roughly [-0.2, 0.2] so it wobbles the boundary.
                var noiseWobble = (noiseField[idx] - 0.5f) * 0.3f;
                var organicDist = Math.Clamp((distance / radius) + noiseWobble, 0f, 1f);

                var elevation = elevations[idx];

                if (riverMap[idx])
                    continue;

                // Base Structural Application based on 'organic' distance
                surfaceFeatures[idx] = volcanoType switch
                {
                    SurfaceFeature.Stratovolcano => organicDist switch
                    {
                        < 0.4f when elevation >= _volcanicCfg.CalderaElevationThreshold => SurfaceFeature.Stratovolcano,
                        > 0.3f and < 0.8f when elevation >= _volcanicCfg.CinderElevationThreshold => SurfaceFeature.Ash,
                        _ => surfaceFeatures[idx]
                    },
                    SurfaceFeature.Shield => organicDist switch
                    {
                        < 0.7f when elevation >= _volcanicCfg.ShieldVolcanoThreshold => SurfaceFeature.Shield,
                        _ => surfaceFeatures[idx]
                    },
                    _ => organicDist switch
                    {
                        < 0.5f when elevation >= _volcanicCfg.CinderElevationThreshold => SurfaceFeature.Cinder,
                        > 0.4f when elevation >= (_volcanicCfg.CinderElevationThreshold - 1) => SurfaceFeature.Ash,
                        _ => surfaceFeatures[idx]
                    }
                };

                // Central Caldera overrides
                if (organicDist < 0.15f && volcanoType == SurfaceFeature.Stratovolcano &&
                    elevation >= _volcanicCfg.CalderaElevationThreshold)
                {
                    surfaceFeatures[idx] = SurfaceFeature.Caldera;
                }
            }
        }

        // 2. TRACE LAVA RIVERS
        // Instead of spawning lava everywhere, we spawn 2 to 5 distinct lava rivers 
        // that start near the peak and flow down the flowmap.
        if (volcanoType is SurfaceFeature.Shield or SurfaceFeature.Stratovolcano)
        {
            var numLavaFlows = _rng.Next(2, 6); // Or derive from strength
            for (var i = 0; i < numLavaFlows; i++)
            {
                // Pick a random starting point slightly offset from the exact center
                var startX = WorldMath.WrapX(centerX + _rng.Next(-2, 3));
                var startY = Math.Clamp(centerY + _rng.Next(-2, 3), 0, _worldHeight - 1);

                var currentX = startX;
                var currentY = startY;
                var lavaVolume = strength * 15f;

                while (lavaVolume > 0)
                {
                    var currentIdx = currentY * _worldWidth + currentX;
                    var dir = flowDirections[currentIdx];

                    // If we hit a pit or flat ground, pool up and stop
                    if (dir.X == 0 && dir.Y == 0) break;

                    var nextX = WorldMath.WrapX(currentX + dir.X);
                    var nextY = currentY + dir.Y;

                    if (nextY < 0 || nextY >= _worldHeight) break; // Off map edge

                    var nextIdx = nextY * _worldWidth + nextX;

                    // Stop if we hit the ocean
                    if (elevations[nextIdx] < _elevationCfg.LandElevationThreshold) break;

                    // Stop if we hit a river (Bonus: turn it to obsidian or rock later?)
                    if (riverMap[nextIdx]) break;

                    // Paint the lava and advance the cursor! (This fixes your infinite loop)
                    surfaceFeatures[nextIdx] = SurfaceFeature.Lava;
                    currentX = nextX;
                    currentY = nextY;
                    lavaVolume -= 1f;
                }
            }
        }

        // 3. APPLY FINAL PEAK CRATER
        // Done last to ensure lava didn't overwrite the very center hole
        var centerIdx = centerY * _worldWidth + centerX;
        if (centerY >= 0 && centerY < _worldHeight && elevations[centerIdx] >= _volcanicCfg.CraterElevationThreshold)
        {
            if (volcanoType is SurfaceFeature.Stratovolcano or SurfaceFeature.Shield)
                surfaceFeatures[centerIdx] = SurfaceFeature.Crater;
            else if (volcanoType == SurfaceFeature.Cinder)
                surfaceFeatures[centerIdx] = SurfaceFeature.Cinder;
        }
    }

    #endregion

    #region Coastal Features

    /// <summary>
    ///     Detect coastal features depending on their surrounding: beaches, cliffs and fjords.
    /// </summary>
    private void ApplyCoastalFeatures(
        Span<float> elevations,
        Span<bool> riverMap,
        Span<SurfaceFeature> surfaceFeatures)
    {
        for (var y = 0; y < _worldHeight; y++)
        {
            var rowOffset = y * _worldWidth;
            for (var x = 0; x < _worldWidth; x++)
            {
                var idx = rowOffset + x;
                // Skip existing assigned strong surface features: river, lava, glacier
                var existing = surfaceFeatures[idx];
                if (existing is SurfaceFeature.Lava || riverMap[idx])
                    continue;

                var elevation = elevations[idx];
                var isWater = elevation < _elevationCfg.LandElevationThreshold;

                // Beach/cliff only for land cells near water
                if (!isWater)
                {
                    var adjacentWater = 0;
                    var maxAdjElevation = 0f;
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        var ny = y + dy;
                        var nRowOffset = ny * _worldWidth;
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            var nx = WorldMath.WrapX(x + dx);
                            if (ny < 0 || ny >= _worldHeight) continue;

                            var neighborElevation = elevations[nRowOffset + nx];
                            if (neighborElevation < _elevationCfg.LandElevationThreshold)
                                adjacentWater++;
                            else
                                maxAdjElevation = Math.Max(maxAdjElevation, neighborElevation);
                        }
                    }

                    if (adjacentWater > 0)
                    {
                        // How high is this coast above the water?
                        var slope = elevation - _elevationCfg.LandElevationThreshold - 1;

                        switch (slope)
                        {
                            // Steep drop into the sea
                            case >= 3.0f:
                                surfaceFeatures[idx] = SurfaceFeature.Cliff;
                                break;
                            // Gentle transition into the sea
                            case <= 1.0f:
                                {
                                    // Don't overwrite existing mountain features from previous steps
                                    if (surfaceFeatures[idx] == SurfaceFeature.None)
                                        surfaceFeatures[idx] = SurfaceFeature.Beach;

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
                    {
                        var ny = y + dy;
                        var nRowOffset = ny * _worldWidth;
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            var nx = WorldMath.WrapX(x + dx);
                            if (ny < 0 || ny >= _worldHeight) continue;
                            var neighborElevation = elevations[nRowOffset + nx];
                            if (neighborElevation < _elevationCfg.LandElevationThreshold) continue;
                            adjacentLand++;
                            if (neighborElevation >= _elevationCfg.SnowThreshold - 1)
                                adjacentHighMountain++;
                        }
                    }

                    if (adjacentLand >= 3 && adjacentHighMountain >= 1)
                        surfaceFeatures[idx] = SurfaceFeature.Fjord;
                }
            }
        }
    }

    #endregion

    #region To ECS Components

    private WorldPackedChunk[] ToPackedChunks(
        Span<float> elevation,
        Span<float> humidity,
        Span<float> temperature,
        Span<Biome> biome,
        Span<SurfaceFeature> surfaceFeatures,
        Span<Point> flowDirections,
        Span<Point> windDirections,
        Span<byte> windSpeeds
    )
    {
        const int chunkSize = WorldMath.ChunkSize;
        const int chunksAcross = WorldMath.ChunksAcross;
        const int worldWidth = WorldMath.WorldWidth;
        const int worldHeight = WorldMath.WorldHeight;

        var chunks = new WorldPackedChunk[chunksAcross * (worldHeight / chunkSize)];
        // Temporary buffer to copy humidity as byte values.
        var elevationBuffer = new byte[chunkSize];
        var humidityBuffer = new byte[chunkSize];

        for (var cy = 0; cy < worldHeight; cy += chunkSize)
        {
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
        }

        return chunks;
    }

    #endregion
}