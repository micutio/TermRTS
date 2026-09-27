using System.Text.Json.Serialization;
using TermRTS.Examples.Greenery.Command;
using TermRTS.Examples.Greenery.Ecs.Component;
using TermRTS.Examples.Greenery.Event;
using TermRTS.Examples.Greenery.Ui;
using TermRTS.Serialization;

namespace TermRTS.Examples.Greenery;

[JsonSourceGenerationOptions(IncludeFields = true)]
[JsonSerializable(typeof(DroneComponent))]
[JsonSerializable(typeof(FovChunk))]
[JsonSerializable(typeof(WorldElevationChunk))]
[JsonSerializable(typeof(WorldSurfaceFeatureChunk))]
[JsonSerializable(typeof(WorldTemperatureChunk))]
[JsonSerializable(typeof(WorldTemperatureAmplitudeChunk))]
[JsonSerializable(typeof(WorldHumidityChunk))]
[JsonSerializable(typeof(WorldBiomeChunk))]
[JsonSerializable(typeof(WorldRiverChunk))]
[JsonSerializable(typeof(WorldPackedChunk))]
[JsonSerializable(typeof(Move))]
[JsonSerializable(typeof(CommandInput))]
[JsonSerializable(typeof(ICommand))]
[JsonSerializable(typeof(ConsoleKeyInfo))]
[JsonSerializable(typeof(MapRenderMode))]
[JsonSerializable(typeof(GoCommand))]
[JsonSerializable(typeof(SaveCommand))]
[JsonSerializable(typeof(LoadCommand))]
public partial class GreeneryJsonContext : JsonSerializerContext
{
    public static PersistenceTypeRegistry CreatePersistenceTypeRegistry()
    {
        var context = Default;
        return new PersistenceTypeRegistry()
            .RegisterComponent<DroneComponent>("greenery.drone", context)
            .RegisterComponent<FovChunk>("greenery.fov-chunk", context)
            .RegisterComponent<WorldElevationChunk>("greenery.world-elevation", context)
            .RegisterComponent<WorldSurfaceFeatureChunk>("greenery.world-surface-feature", context)
            .RegisterComponent<WorldTemperatureChunk>("greenery.world-temperature", context)
            .RegisterComponent<WorldTemperatureAmplitudeChunk>("greenery.world-temperature-amplitude", context)
            .RegisterComponent<WorldHumidityChunk>("greenery.world-humidity", context)
            .RegisterComponent<WorldBiomeChunk>("greenery.world-biome", context)
            .RegisterComponent<WorldRiverChunk>("greenery.world-river", context)
            .RegisterComponent<WorldPackedChunk>("greenery.world-packed", context)
            .RegisterEvent<Move>("greenery.move", context)
            .RegisterEvent<CommandInput>("greenery.command-input", context)
            .RegisterEvent<ICommand>("greenery.command", context)
            .RegisterEvent<ConsoleKeyInfo>("greenery.console-key", context)
            .RegisterEvent<MapRenderMode>("greenery.map-render-mode", context);
    }
}