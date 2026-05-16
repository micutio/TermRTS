using System.Text.Json;
using System.Text.Json.Serialization;

namespace TermRTS.Serialization;

public class ComponentJsonConverter : JsonConverter<ComponentBase>
{
    private const string TypeProperty = "Type";
    private const string EntityProperty = "EntityId";
    private const string DataProperty = "Data";

    public override ComponentBase Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (!root.TryGetProperty(TypeProperty, out var typeProp))
            throw new JsonException($"{TypeProperty} missing");

        var typeName = typeProp.GetString();

        if (typeName == null)
            throw new JsonException("Component type name is null");

        if (!TermRTS.ComponentRegistry.Instance.TryGetDeserializer(typeName, out var deserializer))
            throw new JsonException($"No deserializer registered for component type {typeName}");

        if (!root.TryGetProperty(EntityProperty, out var entityProp))
            throw new JsonException($"{EntityProperty} missing for component {typeName}");

        var entityId = entityProp.GetInt32();

        if (!root.TryGetProperty(DataProperty, out var dataProp))
            throw new JsonException($"{DataProperty} missing for component {typeName}");

        return (ComponentBase)deserializer(dataProp, entityId);
    }

    public override void Write(Utf8JsonWriter writer, ComponentBase value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        var typeName = value.GetType().Name;
        writer.WriteString(TypeProperty, typeName);

        writer.WriteNumber(EntityProperty, value.EntityId);

        if (!TermRTS.ComponentRegistry.Instance.TryGetSerializer(typeName, out var serializer))
        {
            throw new JsonException($"No serializer registered for component type {typeName}");
        }

        writer.WritePropertyName(DataProperty);
        serializer(writer, value as IGameComponent ?? throw new JsonException($"Component {typeName} does not implement IGameComponent"));
        writer.WriteEndObject();
    }
}