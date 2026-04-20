using BenchmarkDotNet.Attributes;
using TermRTS.Examples.Greenery.WorldGen;

namespace TermRTS.Benchmark;

[MemoryDiagnoser]
public class PackBenchmarks
{
    private Biome[]? _biome;
    private byte[]? _elevation;
    private float[]? _temperature;
    private byte[]? _humidity;
    private (int x, int y)[]? _waterflow;
    private (int x, int y)[]? _wind;
    private byte[]? _windSpeed;
    private SurfaceFeature[]? _feature;
    private PackedTile[]? _destRow;
    private int _length;

    [GlobalSetup]
    public void Setup()
    {
        _length = WorldMath.ChunkSize;
        var rnd = new Random(12345);

        _biome = new Biome[_length];
        _elevation = new byte[_length];
        _temperature = new float[_length];
        _humidity = new byte[_length];
        _waterflow = new (int x, int y)[_length];
        _wind = new (int x, int y)[_length];
        _windSpeed = new byte[_length];
        _feature = new SurfaceFeature[_length];
        _destRow = new PackedTile[_length];

        var biomeValues = Enum.GetValues(typeof(Biome));
        var featValues = Enum.GetValues(typeof(SurfaceFeature));

        for (var i = 0; i < _length; i++)
        {
            _biome[i] = (Biome)biomeValues.GetValue(rnd.Next(biomeValues.Length))!;
            _elevation[i] = (byte)rnd.Next(0, 256);
            _temperature[i] = rnd.Next(-40, 50);
            _humidity[i] = (byte)rnd.Next(0, 101);
            _waterflow[i] = (rnd.Next(-1, 2), rnd.Next(-1, 2));
            _wind[i] = (rnd.Next(-1, 2), rnd.Next(-1, 2));
            _windSpeed[i] = (byte)rnd.Next(0, 256);
            _feature[i] = (SurfaceFeature)featValues.GetValue(rnd.Next(featValues.Length))!;
        }
    }

    [Benchmark(Baseline = true)]
    public PackedTile[] Pack_Allocating()
    {
        var result = WorldPacker.Pack(
            _biome,
            _elevation,
            _temperature,
            _humidity,
            _waterflow,
            _wind,
            _windSpeed,
            _feature);

        GC.KeepAlive(result);
        return result;
    }

    [Benchmark]
    public void PackPooled_RentAndReturn()
    {
        var buffer = WorldPacker.PackPooled(
            _biome,
            _elevation,
            _temperature,
            _humidity,
            _waterflow,
            _wind,
            _windSpeed,
            _feature);

        WorldPacker.ReturnPackedArray(buffer, clearArray: false);
    }

    [Benchmark]
    public void PackToSpan_DirectIntoPreallocated()
    {
        WorldPacker.PackToSpan(
            _destRow.AsSpan(),
            _biome,
            _elevation,
            _temperature,
            _humidity,
            _waterflow,
            _wind,
            _windSpeed,
            _feature);

        GC.KeepAlive(_destRow);
    }
}