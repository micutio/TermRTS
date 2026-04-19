using System.Buffers;
using System.Runtime.CompilerServices;
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

    public (int x, int y) WaterFlow => (
        (PackedVectors >> 0 & 0x3) - 1,
        (PackedVectors >> 2 & 0x3) - 1);

    public (int x, int y) Wind => (
        (PackedVectors >> 4 & 0x3) - 1,
        (PackedVectors >> 6 & 0x3) - 1);

    #endregion

    #region Setters

    public void SetInFov()
    {
        VisibilityFlags |= VisibilityMasks.InFOV;
    }

    public void SetOutOfFov()
    {
        VisibilityFlags &= unchecked((byte)~VisibilityMasks.InFOV);
    }

    public void SetExplored()
    {
        VisibilityFlags |= VisibilityMasks.ExploredFOW;
    }

    #endregion
}

/// <summary>
/// Packs multiple world data sets into one single packed array.
/// </summary>
public static class WorldPacker
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
        if (length != elevations.Length || length != temperatures.Length ||
            length != humidities.Length || length != waterflows.Length ||
            length != winds.Length || length != features.Length)
        {
            throw new ArgumentException("All input spans must have the same length.");
        }

        var packedMap = new PackedTile[length];

        for (var i = 0; i < packedMap.Length; i++)
        {
            packedMap[i] = new PackedTile
            {
                PackedAttributes = PackAttributes(
                    biomes[i],
                    elevations[i],
                    humidities[i],
                    features[i]),
                Temperature = PackTemperature(temperatures[i]),
                VisibilityFlags = 0,
                PackedVectors = PackVectors(waterflows[i], winds[i])
            };
        }

        return packedMap;
    }

    public static void Unpack(
        PackedTile packed,
        out Biome biome,
        out byte elevation,
        out byte humidity,
        out sbyte temperature,
        out (int x, int y) waterflow,
        out (int x, int y) wind,
        out SurfaceFeature feature)
    {
        biome = packed.Biome;
        elevation = packed.Elevation;
        humidity = packed.Humidity;
        temperature = packed.Temperature;
        feature = packed.SurfaceFeature;
        waterflow = packed.WaterFlow;
        wind = packed.Wind;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint PackAttributes(
        Biome biome,
        byte elevation,
        byte humidity,
        SurfaceFeature feature)
    {
        return (uint)biome << PackedAttribute.BiomeShift |
               (uint)elevation << PackedAttribute.ElevShift |
               (uint)humidity << PackedAttribute.HumidShift |
               (uint)feature << PackedAttribute.FeatShift;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static sbyte PackTemperature(float temperature)
    {
        // Clamp instead of throwing to avoid exceptions on occasional out-of-range
        // values. This keeps the hot path exception-free and predictable.
        var clamped = temperature;
        if (clamped < sbyte.MinValue) clamped = sbyte.MinValue;
        else if (clamped > sbyte.MaxValue) clamped = sbyte.MaxValue;

        return Convert.ToSByte(clamped);
    }

    /// <summary>
    /// Packs into a pooled array rented from <see cref="ArrayPool{PackedTile}.Shared"/>.
    /// Caller must return the array with <see cref="ReturnPackedArray(PackedTile[],bool)"/> when done.
    /// The returned array length may be larger than the requested length; only the first
    /// <paramref name="biomes"/>.Length elements are valid.
    /// </summary>
    public static PackedTile[] PackPooled(
        ReadOnlySpan<Biome> biomes,
        ReadOnlySpan<byte> elevations,
        ReadOnlySpan<float> temperatures,
        ReadOnlySpan<byte> humidities,
        ReadOnlySpan<(int x, int y)> waterflows,
        ReadOnlySpan<(int x, int y)> winds,
        ReadOnlySpan<SurfaceFeature> features)
    {
        var length = biomes.Length;
        if (length != elevations.Length || length != temperatures.Length ||
            length != humidities.Length || length != waterflows.Length ||
            length != winds.Length || length != features.Length)
        {
            throw new ArgumentException("All input spans must have the same length.");
        }

        var pool = ArrayPool<PackedTile>.Shared;
        var buffer = pool.Rent(length);

        for (var i = 0; i < length; i++)
        {
            buffer[i] = new PackedTile
            {
                PackedAttributes = PackAttributes(
                    biomes[i],
                    elevations[i],
                    humidities[i],
                    features[i]),
                Temperature = PackTemperature(temperatures[i]),
                VisibilityFlags = 0,
                PackedVectors = PackVectors(waterflows[i], winds[i])
            };
        }

        return buffer;
    }

    /// <summary>
    /// Return a pooled array obtained from <see cref="PackPooled"/> back to the shared pool.
    /// If <paramref name="clearArray"/> is true the array will be cleared before returning.
    /// </summary>
    public static void ReturnPackedArray(PackedTile[] buffer, bool clearArray = false)
    {
        if (buffer is null) return;
        ArrayPool<PackedTile>.Shared.Return(buffer, clearArray);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte PackVectors(
        (int x, int y) waterflow,
        (int x, int y) wind)
    {
        ValidateDirection(waterflow.x, nameof(waterflow));
        ValidateDirection(waterflow.y, nameof(waterflow));
        ValidateDirection(wind.x, nameof(wind));
        ValidateDirection(wind.y, nameof(wind));

        return (byte)(
            (waterflow.x + 1 & 0x3) |
            ((waterflow.y + 1 & 0x3) << 2) |
            ((wind.x + 1 & 0x3) << 4) |
            ((wind.y + 1 & 0x3) << 6));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateDirection(int direction, string name)
    {
        if (direction is < -1 or > 1)
        {
            throw new ArgumentOutOfRangeException(name,
                "Direction values must be -1, 0 or 1.");
        }
    }
}