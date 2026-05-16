using System;
using System.Numerics;

namespace TermRTS.Examples.Greenery.Serialization;

public struct DroneData
{
    public float X { get; set; }
    public float Y { get; set; }
}

public struct FovChunkData
{
    public int Cx { get; set; }
    public int Cy { get; set; }
    public bool[] FovField { get; set; }
}

public struct WorldElevationChunkData
{
    public int Cx { get; set; }
    public int Cy { get; set; }
    public int[] Elevation { get; set; }
}

public struct WorldSurfaceFeatureChunkData
{
    public int Cx { get; set; }
    public int Cy { get; set; }
    public TermRTS.Examples.Greenery.WorldGen.SurfaceFeature[] SurfaceFeature { get; set; }
}

public struct WorldTemperatureChunkData
{
    public int Cx { get; set; }
    public int Cy { get; set; }
    public float[] Temperature { get; set; }
}

public struct WorldTemperatureAmplitudeChunkData
{
    public int Cx { get; set; }
    public int Cy { get; set; }
    public float[] TemperatureAmplitude { get; set; }
}

public struct WorldHumidityChunkData
{
    public int Cx { get; set; }
    public int Cy { get; set; }
    public float[] Humidity { get; set; }
}

public struct WorldBiomeChunkData
{
    public int Cx { get; set; }
    public int Cy { get; set; }
    public TermRTS.Examples.Greenery.WorldGen.Biome[] Biome { get; set; }
}

public struct WorldRiverChunkData
{
    public int Cx { get; set; }
    public int Cy { get; set; }
    public bool[] River { get; set; }
}

public struct WorldPackedChunkData
{
    public int Cx { get; set; }
    public int Cy { get; set; }
    public TermRTS.Examples.Greenery.WorldGen.PackedTile[] PackedTiles { get; set; }
}