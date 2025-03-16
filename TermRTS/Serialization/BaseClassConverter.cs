using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace TermRTS.Serialization;

/// <summary>
/// Static untyped variant of <see cref="BaseClassConverter{TBaseType}"/> to act as a factory for
/// easier instantiation.
/// </summary>
public static class BaseClassConverter
{
    /// <summary>
    /// Shorthand to create a new converter for a given baseclass
    /// </summary>
    /// <typeparam name="T">Type of interface or abstract class</typeparam>
    /// <returns>New BaseClassConverter instance for <see cref="T"/></returns>
    public static BaseClassConverter<T> GetForType<T>() where T : class
    {
        return new BaseClassConverter<T>(GetAllSubTypes<T>());
    }

    #region Private Members

    private static Type[] GetAllSubTypes<TSuperType>()
    {
        var types = AppDomain
            .CurrentDomain
            .GetAssemblies()
            .SelectMany(x => x.GetTypes())
            .Where(x => typeof(TSuperType).IsAssignableFrom(x) && x is { IsInterface: false, IsAbstract: false })
            .ToArray();
        return types;
    }

    #endregion
}

public class BaseClassConverter<TBaseType>(params Type[] types) : JsonConverter<TBaseType>
    where TBaseType : class
{
    private const string TypeProperty = "$type";


    public override bool CanConvert(Type typeToConvert)
    {
        // only responsible for the abstract base
        return typeof(TBaseType) == typeToConvert;
    }

    #region JsonConverter<> Members

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
                var type = Array.Find(types, t => t.Name == typeName) ??
                           throw new JsonException($"{TypeProperty} specifies an invalid type");

                var rootElement = doc.RootElement.GetRawText();
                result = JsonSerializer.Deserialize(rootElement, type, options) as TBaseType ??
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
        if (Array.Exists(types, t => type.Name == t.Name))
        {
            var jsonElement = JsonSerializer.SerializeToElement(value, type, options);

            var jsonObject = JsonObject.Create(jsonElement) ?? throw new JsonException();
            jsonObject[TypeProperty] = type.Name;

            jsonObject.WriteTo(writer, options);
        }
        else
        {
            throw new JsonException($"{type.Name} with matching base type {typeof(TBaseType).Name} is not registered.");
        }
    }

    #endregion
}