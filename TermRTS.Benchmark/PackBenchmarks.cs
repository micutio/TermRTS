using BenchmarkDotNet.Attributes;
using TermRTS.Examples.Greenery.WorldGen;

namespace TermRTS.Benchmark;

[MemoryDiagnoser]
public class PackBenchmarks
{
    private Biome[] _biomes;
    private byte[] _elevations;
    private float[] _temperatures;
    private byte[] _humidities;
    private (int x, int y)[] _waterflows;
    private (int x, int y)[] _winds;
    private byte[] _windSpeeds;
    private SurfaceFeature[] _features;
    private PackedTile[] _destRow;
    private int _length;

    [GlobalSetup]
    public void Setup()
    {
        _length = WorldMath.ChunkSize;
        var rnd = new Random(12345);

        _biomes = new Biome[_length];
        _elevations = new byte[_length];
        _temperatures = new float[_length];
        _humidities = new byte[_length];
        _waterflows = new (int x, int y)[_length];
        _winds = new (int x, int y)[_length];
        _windSpeeds = new byte[_length];
        _features = new SurfaceFeature[_length];
        _destRow = new PackedTile[_length];

        var biomeValues = Enum.GetValues(typeof(Biome));
        var featValues = Enum.GetValues(typeof(SurfaceFeature));

        for (var i = 0; i < _length; i++)
        {
            _biomes[i] = (Biome)biomeValues.GetValue(rnd.Next(biomeValues.Length))!;
            _elevations[i] = (byte)rnd.Next(0, 256);
            _temperatures[i] = rnd.Next(-40, 50);
            _humidities[i] = (byte)rnd.Next(0, 101);
            _waterflows[i] = (rnd.Next(-1, 2), rnd.Next(-1, 2));
            _winds[i] = (rnd.Next(-1, 2), rnd.Next(-1, 2));
            _windSpeeds[i] = (byte)rnd.Next(0, 256);
            _features[i] = (SurfaceFeature)featValues.GetValue(rnd.Next(featValues.Length))!;
        }
    }

    [Benchmark(Baseline = true)]
    public PackedTile[] Pack_Allocating()
    {
        var result = WorldPacker.Pack(
            _biomes,
            _elevations,
            _temperatures,
            _humidities,
            _waterflows,
            _winds,
            _windSpeeds,
            _features);

        GC.KeepAlive(result);
        return result;
    }

    [Benchmark]
    public void PackPooled_RentAndReturn()
    {
        var buffer = WorldPacker.PackPooled(
            _biomes,
            _elevations,
            _temperatures,
            _humidities,
            _waterflows,
            _winds,
            _windSpeeds,
            _features);

        WorldPacker.ReturnPackedArray(buffer, clearArray: false);
    }

    [Benchmark]
    public void PackToSpan_DirectIntoPreallocated()
    {
        WorldPacker.PackToSpan(
            _destRow.AsSpan(),
            _biomes,
            _elevations,
            _temperatures,
            _humidities,
            _waterflows,
            _winds,
            _windSpeeds,
            _features);

        GC.KeepAlive(_destRow);
    }
}