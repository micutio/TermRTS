using TermRTS.Examples.Greenery.WorldGen;

namespace TermRTS.Examples.Greenery.Ecs.Component;

public sealed class WorldElevationChunk(int entityId, int cx, int cy, int[] elevation)
    : ComponentBase(entityId)
{
    public readonly int Cx = cx;
    public readonly int Cy = cy;
    public readonly int[] Elevation = elevation;
}

public sealed class WorldSurfaceFeatureChunk(
    int entityId,
    int cx,
    int cy,
    SurfaceFeature[] surfaceFeature
)
    : ComponentBase(entityId)
{
    public readonly int Cx = cx;
    public readonly int Cy = cy;
    public readonly SurfaceFeature[] SurfaceFeature = surfaceFeature;
}

public sealed class WorldTemperatureChunk(
    int entityId,
    int cx,
    int cy,
    float[] temperature)
    : ComponentBase(entityId)
{
    public readonly int Cx = cx;
    public readonly int Cy = cy;
    public readonly float[] Temperature = temperature;
}

public sealed class WorldTemperatureAmplitudeChunk(
    int entityId,
    int cx,
    int cy,
    float[] temperatureAmplitude)
    : ComponentBase(entityId)
{
    public readonly int Cx = cx;
    public readonly int Cy = cy;
    public readonly float[] TemperatureAmplitude = temperatureAmplitude;
}

public sealed class WorldHumidityChunk(
    int entityId,
    int cx,
    int cy,
    float[] humidity)
    : ComponentBase(entityId)
{
    public readonly int Cx = cx;
    public readonly int Cy = cy;
    public readonly float[] Humidity = humidity;
}

public sealed class WorldBiomeChunk(
    int entityId,
    int cx,
    int cy,
    Biome[] biome)
    : ComponentBase(entityId)
{
    public readonly int Cx = cx;
    public readonly int Cy = cy;
    public readonly Biome[] Biome = biome;
}

public sealed class WorldRiverChunk(int entityId, int cx, int cy, bool[] river)
    : ComponentBase(entityId)
{
    public readonly int Cx = cx;
    public readonly int Cy = cy;
    public readonly bool[] River = river;
}

public sealed class WorldPackedChunk(int entityId, int cx, int cy, PackedTile[] packedTiles)
    : ComponentBase(entityId)
{
    public readonly int Cx = cx;
    public readonly int Cy = cy;
    public readonly PackedTile[] PackedTiles = packedTiles;
}