using System.Runtime.InteropServices;

namespace TermRTS.Examples.Greenery.WorldGen;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PackedTile
{
    internal const int BiomeShift = 0;
    internal const int ElevShift = 8;
    internal const int HumidShift = 16;
    internal const int FeatShift = 24;

    // 4 bytes: Biome, Elevation, Humidity, Surface Feature
    public uint PackedAttributes;

    // 4 bytes
    public float Temperature;

    // 4 bytes: 2 vectors (X, Y) using 1 byte per component
    public sbyte FlowX;
    public sbyte FlowY;
    public sbyte WindX;
    public sbyte WindY;

    public Biome Biome => (Biome)(PackedAttributes & 0xFF);
    public byte Elevation => (byte)(PackedAttributes >> ElevShift & 0xFF);
    public byte Humidity => (byte)(PackedAttributes >> HumidShift & 0xFF);
    public SurfaceFeature SurfaceFeature => (SurfaceFeature)(PackedAttributes >> FeatShift & 0xFF);
}

/// <summary>
/// Packs multiple world data sets into one single packed array.
/// </summary>
public class WorldPacker
{
    public static PackedTile[] Pack(
        ReadOnlySpan<Biome> biomes,
        ReadOnlySpan<byte> elevations,
        ReadOnlySpan<float> temperatures,
        ReadOnlySpan<byte> humidities,
        ReadOnlySpan<(int x, int y)> waterflows,
        ReadOnlySpan<(int x, int y)> winds,
        ReadOnlySpan<SurfaceFeature> features)
    {
        var length = biomes.Length;
        var packedMap = new PackedTile[length];

        for (var i = 0; i < length; i++)
        {
            var attr = (uint)biomes[i] << PackedTile.BiomeShift |
                       (uint)elevations[i] << PackedTile.ElevShift |
                       (uint)humidities[i] << PackedTile.HumidShift |
                       (uint)features[i] << PackedTile.FeatShift;

            packedMap[i] = new PackedTile
            {
                PackedAttributes = attr,
                Temperature = temperatures[i],
                FlowX = (sbyte)waterflows[i].x,
                FlowY = (sbyte)waterflows[i].y,
                WindX = (sbyte)winds[i].x,
                WindY = (sbyte)winds[i].y
            };
        }

        return packedMap;
    }

    public static void Unpack(
        ReadOnlySpan<PackedTile> packedData,
        Span<byte> biomes,
        Span<byte> elevations,
        Span<float> temperatures,
        Span<byte> humidities,
        Span<(int x, int y)> waterflows,
        Span<(int x, int y)> winds,
        Span<byte> features)
    {
        for (var i = 0; i < packedData.Length; i++)
        {
            ref readonly var tile = ref packedData[i];
            var attr = tile.PackedAttributes;

            biomes[i] = (byte)(attr & 0xFF);
            elevations[i] = (byte)(attr >> PackedTile.ElevShift & 0xFF);
            humidities[i] = (byte)(attr >> PackedTile.HumidShift & 0xFF);
            features[i] = (byte)(attr >> PackedTile.FeatShift & 0xFF);

            temperatures[i] = tile.Temperature;
            waterflows[i] = (tile.FlowX, tile.FlowY);
            winds[i] = (tile.WindX, tile.WindY);
        }
    }
}