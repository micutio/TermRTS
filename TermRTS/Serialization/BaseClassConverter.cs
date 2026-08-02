using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace TermRTS.Serialization;

/// <summary>
///     Static untyped variant of <see cref="BaseClassConverter{TBaseType}" /> to act as a factory for
///     easier instantiation.
/// </summary>
public static class BaseClassConverter
{
    /// <summary>
    ///     Shorthand to create a new converter for a given baseclass using a fixed set of concrete types.
    /// </summary>
    /// <typeparam name="T">Type of interface or abstract class</typeparam>
    /// <param name="types">Concrete types that can be deserialized for <see cref="T" /></param>
    /// <returns>New BaseClassConverter instance for <see cref="T" /></returns>
    public static BaseClassConverter<T> GetForType<T>(params Type[] types) where T : class
    {
        return new BaseClassConverter<T>(types);
    }

}

public class BaseClassConverter<TBaseType>(params Type[] types) : JsonConverter<TBaseType>
    where TBaseType : class
{
    private const string TypeProperty = "$type";

    private static Type? ResolveType(string? typeName, IReadOnlyList<Type> registeredTypes)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return null;
        }

        return registeredTypes.FirstOrDefault(t =>
            string.Equals(t.FullName, typeName, StringComparison.Ordinal) ||
            string.Equals(t.Name, typeName, StringComparison.Ordinal) ||
            string.Equals(t.AssemblyQualifiedName, typeName, StringComparison.Ordinal));
    }

    public override bool CanConvert(Type typeToConvert)
    {
        // only responsible for the abstract base
        return typeof(TBaseType) == typeToConvert;
    }

    public override TBaseType Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        TBaseType result;

        if (JsonDocument.TryParseValue(ref reader, out var doc))
        {
            if (doc.RootElement.TryGetProperty(TypeProperty, out var typeProperty))
            {
                var typeName = typeProperty.GetString();
                var type = ResolveType(typeName, types);

                if (type is null)
                    throw new JsonException($"{TypeProperty} specifies an invalid type");

                var rootElement = doc.RootElement.GetRawText();
                var typeInfo = options.GetTypeInfo(type);
                result = JsonSerializer.Deserialize(rootElement, typeInfo) as TBaseType ??
                         throw new JsonException("target type could not be serialized");
            }
            else
            {
                throw new JsonException($"{TypeProperty} missing");
            }
        }
        else
        {
            throw new JsonException("Failed to parse JsonDocument");
        }

        return result;
    }

    public override void Write(
        Utf8JsonWriter writer,
        TBaseType value,
        JsonSerializerOptions options)
    {
        var type = value.GetType();
        var discriminator = type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
        var typeInfo = options.GetTypeInfo(type);
        var jsonElement = JsonSerializer.SerializeToElement(value, typeInfo);

        var jsonObject = JsonObject.Create(jsonElement) ?? throw new JsonException();
        jsonObject[TypeProperty] = discriminator;

        jsonObject.WriteTo(writer, options);
    }
}