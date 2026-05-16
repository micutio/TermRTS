using System.Text.Json.Serialization;
using System.Text.Json;
using TermRTS.Examples.Greenery.Serialization;

namespace TermRTS.Examples.Greenery.Serialization;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(DroneData))]
[JsonSerializable(typeof(FovChunkData))]
[JsonSerializable(typeof(WorldElevationChunkData))]
[JsonSerializable(typeof(WorldSurfaceFeatureChunkData))]
[JsonSerializable(typeof(WorldTemperatureChunkData))]
[JsonSerializable(typeof(WorldTemperatureAmplitudeChunkData))]
[JsonSerializable(typeof(WorldHumidityChunkData))]
[JsonSerializable(typeof(WorldBiomeChunkData))]
[JsonSerializable(typeof(WorldRiverChunkData))]
[JsonSerializable(typeof(WorldPackedChunkData))]
public partial class GreeneryJsonContext : JsonSerializerContext
{
}