using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using TermRTS;

public class ComponentConverter : JsonConverter<ComponentBase>
{
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

            string typeName = typeProperty.GetString();
            Type type = _registry.GetType(typeName);

            if (type == null)
            {
                throw new JsonException($"Unknown type: {typeName}");
            }

            return JsonSerializer.Deserialize(document.RootElement.ToString(), type,
                options); // Deserialize using the resolved type
        }
    }

    public override void Write(Utf8JsonWriter writer, ComponentBase value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteString("Type", value.GetType().Name); // Important: Add type discriminator
        writer.WriteString("Name", value.Name);

        if (value is Dog dog)
            writer.WriteString("Breed", dog.Breed);
        else if (value is Cat cat) writer.WriteString("Color", cat.Color);

        writer.WriteEndObject();
    }

    public class AnimalTypeRegistry
    {
        private Dictionary<string, Type> _typeMap = new();

        public AnimalTypeRegistry()
        {
            // Register known types explicitly (or use assembly scanning - see below)
            // RegisterType(nameof(Dog), typeof(Dog));
            // RegisterType(nameof(Cat), typeof(Cat));
        }

        public void RegisterType(string typeName, Type type)
        {
            _typeMap[typeName] = type;
        }

        public Type? GetType(string typeName)
        {
            return _typeMap.GetValueOrDefault(typeName); // Or throw an exception
        }

        // Optional: Assembly scanning for automatic registration
        public void RegisterTypesFromAssembly(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
                if (typeof(ComponentBase).IsAssignableFrom(type) && !type.IsInterface &&
                    !type.IsAbstract) // Check interface and exclude interfaces and abstract classes
                    _typeMap[type.Name] = type;
        }
    }
}