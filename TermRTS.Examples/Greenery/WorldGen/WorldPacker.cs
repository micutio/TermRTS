using System.Runtime.InteropServices;

namespace TermRTS.Examples.Greenery.WorldGen;

public static class VisibilityMasks
{
    public const byte InFOV = 1 << 0; // 0000_0001
    public const byte ExploredFOW = 1 << 1; // 0000_0010
}

public static class PackedAttribute
{
    public const int BiomeShift = 0;
    public const int ElevShift = 8;
    public const int HumidShift = 16;
    public const int FeatShift = 24;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PackedTile
{
    #region Fields

    // 4 Bytes: Biome, Elevation, Humidity, Surface Feature
    public uint PackedAttributes;

    // 1 Byte (-128 to +127 Celsius) 
    public sbyte Temperature;

    // 1 Byte (FOV, FOW, and six more bools)
    public byte VisibilityFlags;

    // 1 Byte (Holds FlowX, FlowY, WindX, WindY)
    public byte PackedVectors;

    // 1 Byte padding to hit exactly 8 Bytes. Usable for more fields.
    public byte Reserved;

    #endregion

    #region Properties

    public Biome Biome => (Biome)(PackedAttributes >> PackedAttribute.BiomeShift & 0xFF);
    public byte Elevation => (byte)(PackedAttributes >> PackedAttribute.ElevShift & 0xFF);
    public byte Humidity => (byte)(PackedAttributes >> PackedAttribute.HumidShift & 0xFF);

    public SurfaceFeature SurfaceFeature =>
        (SurfaceFeature)(PackedAttributes >> PackedAttribute.FeatShift & 0xFF);

    public bool IsVisible => (VisibilityFlags & VisibilityMasks.InFOV) != 0;
    public bool IsExplored => (VisibilityFlags & VisibilityMasks.ExploredFOW) != 0;

    #endregion

    #region Setters

    public void SetInFov()
    {
        VisibilityFlags &= VisibilityMasks.InFOV;
    }

    public void SetOutOfFov()
    {
        VisibilityFlags &= unchecked((byte)~VisibilityMasks.InFOV);
    }

    public void SetExplored()
    {
        VisibilityFlags |= VisibilityMasks.InFOV;
    }

    #endregion
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
            var attr = (uint)biomes[i] << PackedAttribute.BiomeShift |
                       (uint)elevations[i] << PackedAttribute.ElevShift |
                       (uint)humidities[i] << PackedAttribute.HumidShift |
                       (uint)features[i] << PackedAttribute.FeatShift;

            var visibilityFlags = (byte)0;

            var packedVectors = (byte)(
                waterflows[i].x + 1 & 0x3 |
                (waterflows[i].y + 1 & 0x3) << 2 |
                (winds[i].x + 1 & 0x3) << 4 |
                (winds[i].y + 1 & 0x3) << 6);

            packedMap[i] = new PackedTile
            {
                PackedAttributes = attr,
                Temperature = Convert.ToSByte(temperatures[i]),
                VisibilityFlags = visibilityFlags,
                PackedVectors = packedVectors
            };
        }

        return packedMap;
    }
}