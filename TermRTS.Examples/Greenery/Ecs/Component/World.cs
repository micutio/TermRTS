using TermRTS.Examples.Greenery.WorldGen;

namespace TermRTS.Examples.Greenery.Ecs.Component;


public sealed class WorldElevationChunk(int entityId, int cx, int cy, ReadOnlyMemory<int> elevation)
    : ComponentBase(entityId)
{
    public readonly int Cx = cx;
    public readonly int Cy = cy;
    public readonly ReadOnlyMemory<int> Elevation = elevation;
}

public sealed class WorldSurfaceFeatureChunk(
    int entityId,
    int cx,
    int cy,
    ReadOnlyMemory<SurfaceFeature> surfaceFeature
)
    : ComponentBase(entityId)
{
    public readonly int Cx = cx;
    public readonly int Cy = cy;
    public readonly ReadOnlyMemory<SurfaceFeature> SurfaceFeature = surfaceFeature;
}

public sealed class WorldTemperatureChunk(
    int entityId,
    int cx,
    int cy,
    ReadOnlyMemory<float> temperature)
    : ComponentBase(entityId)
{
    public readonly int Cx = cx;
    public readonly int Cy = cy;
    public readonly ReadOnlyMemory<float> Temperature = temperature;
}

public sealed class WorldTemperatureAmplitudeChunk(
    int entityId,
    int cx,
    int cy,
    ReadOnlyMemory<float> temperatureAmplitude)
    : ComponentBase(entityId)
{
    public readonly int Cx = cx;
    public readonly int Cy = cy;
    public readonly ReadOnlyMemory<float> TemperatureAmplitude = temperatureAmplitude;
}

public sealed class WorldHumidityChunk(
    int entityId,
    int cx,
    int cy,
    ReadOnlyMemory<float> humidity)
    : ComponentBase(entityId)
{
    public readonly int Cx = cx;
    public readonly int Cy = cy;
    public readonly ReadOnlyMemory<float> Humidity = humidity;
}

public sealed class WorldBiomeChunk(
    int entityId,
    int cx,
    int cy,
    ReadOnlyMemory<Biome> biome)
    : ComponentBase(entityId)
{
    public readonly int Cx = cx;
    public readonly int Cy = cy;
    public readonly ReadOnlyMemory<Biome> Biome = biome;
}

public sealed class WorldRiverChunk(int entityId, int cx, int cy, ReadOnlyMemory<bool> river)
    : ComponentBase(entityId)
{
    public readonly int Cx = cx;
    public readonly int Cy = cy;
    public readonly ReadOnlyMemory<bool> River = river;
}