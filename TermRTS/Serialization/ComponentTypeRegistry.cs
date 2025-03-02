using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TermRTS.Serialization;

public class ComponentTypeRegistry
{
    private readonly Dictionary<string, Type> _typeMap = new();

    public ComponentTypeRegistry()
    {
        RegisterTypesFromAssembly(Assembly.GetExecutingAssembly());
    }

    public void RegisterTypesFromAssembly(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
            if (typeof(ComponentBase).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                _typeMap.Add(type.FullName, type);
    }

    public void RegisterType(string typeName, Type type)
    {
        _typeMap[typeName] = type;
    }

    public Type GetType(string typeName)
    {
        if (_typeMap.TryGetValue(typeName, out var type)) return type;
        return null;
    }
}

public class ComponentConverter : JsonConverter<ComponentBase>
{
    private readonly ComponentTypeRegistry _registry = new();

    public override bool CanConvert(Type typeToConvert)
    {
        return typeof(ComponentBase).IsAssignableFrom(typeToConvert);
    }

    public override ComponentBase Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();

        using (var document = JsonDocument.ParseValue(ref reader))
        {
            if (!document.RootElement.TryGetProperty("Type", out var typeProperty))
                throw new JsonException("Type property is missing.");

            var typeName = typeProperty.GetString();
            var type = _registry.GetType(typeName);

            if (type == null) throw new JsonException($"Type '{typeName}' does not exist.");

            return (ComponentBase)JsonSerializer.Deserialize(document.RootElement.ToString(), type, options);
        }
    }

    public override void Write(Utf8JsonWriter writer, ComponentBase value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Type", value.GetType().Name);
        writer.WriteString("Name", value.ToString());
    }
}