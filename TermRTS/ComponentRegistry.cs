using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace TermRTS;

public sealed class ComponentRegistry
{
    private readonly Dictionary<string, Action<Utf8JsonWriter, IGameComponent>> _serializers
        = new();

    // Deserializers receive the component's stored JsonElement and the entity id read from the envelope
    private readonly Dictionary<string, Func<JsonElement, int, IGameComponent>> _deserializers
        = new();

    public static ComponentRegistry Instance { get; } = new ComponentRegistry();

    private ComponentRegistry()
    {
    }

    public void Register<TComp, TData>(JsonTypeInfo<TData> typeInfo)
        where TComp : ComponentBase<TData>, new()
        where TData : struct
    {
        var typeName = typeof(TComp).Name;

        _serializers[typeName] = (writer, comp) =>
        {
            var typed = (TComp)comp;
            var data = typed.GetCurrentData();
            JsonSerializer.Serialize(writer, data, typeInfo);
        };

        _deserializers[typeName] = (jsonElement, entityId) =>
        {
            var data = JsonSerializer.Deserialize(jsonElement, typeInfo);
            var comp = new TComp();
            comp.EntityId = entityId;
            // Use untyped setter from IGameComponent to avoid generic-awareness here
            ((IGameComponent)comp).SetCurrentStateUntyped(data!);
            return (IGameComponent)comp;
        };
    }

    /// <summary>
    /// Register a non-generic component type by providing extractor and factory functions.
    /// The extractor converts a component instance into its serializable data, and the
    /// factory creates a new component instance from the entity id and deserialized data.
    /// </summary>
    public void Register<TComp, TData>(JsonTypeInfo<TData> typeInfo, Func<TComp, TData> extractor, Func<int, TData, TComp> factory)
        where TComp : ComponentBase
    {
        var typeName = typeof(TComp).Name;

        _serializers[typeName] = (writer, comp) =>
        {
            var typed = (TComp)comp;
            var data = extractor(typed);
            JsonSerializer.Serialize(writer, data, typeInfo);
        };

        _deserializers[typeName] = (jsonElement, entityId) =>
        {
            var data = JsonSerializer.Deserialize(jsonElement, typeInfo)!;
            var comp = factory(entityId, data);
            return (IGameComponent)comp;
        };
    }

    public bool TryGetSerializer(string typeName, out Action<Utf8JsonWriter, IGameComponent>? serializer)
    {
        return _serializers.TryGetValue(typeName, out serializer);
    }

    public bool TryGetDeserializer(string typeName, out Func<JsonElement, int, IGameComponent>? deserializer)
    {
        return _deserializers.TryGetValue(typeName, out deserializer);
    }
}