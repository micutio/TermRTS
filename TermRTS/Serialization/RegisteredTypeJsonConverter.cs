using System.Text.Json;
using System.Text.Json.Serialization;
using TermRTS.Event;

namespace TermRTS.Serialization;

internal sealed class RegisteredTypeJsonConverter<TBaseType> : JsonConverter<TBaseType>
{
    private const string TypePropertyName = "$type";
    private const string ValuePropertyName = "$value";

    private readonly IReadOnlyDictionary<string, PersistenceTypeRegistration> _byDiscriminator;
    private readonly IReadOnlyDictionary<Type, PersistenceTypeRegistration> _byType;

    public RegisteredTypeJsonConverter(IReadOnlyList<PersistenceTypeRegistration> registrations)
    {
        _byDiscriminator = registrations.ToDictionary(
            registration => registration.Discriminator,
            StringComparer.Ordinal);
        _byType = registrations.ToDictionary(registration => registration.TypeInfo.Type);
    }

    public override TBaseType Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (!root.TryGetProperty(TypePropertyName, out var typeProperty) ||
            typeProperty.ValueKind != JsonValueKind.String)
        {
            throw new JsonException($"Missing string discriminator '{TypePropertyName}'.");
        }

        var discriminator = typeProperty.GetString();
        if (discriminator is null ||
            !_byDiscriminator.TryGetValue(discriminator, out var registration))
        {
            throw new JsonException($"Unregistered persistence discriminator '{discriminator}'.");
        }

        if (!root.TryGetProperty(ValuePropertyName, out var value))
        {
            throw new JsonException($"Missing persistence value '{ValuePropertyName}'.");
        }

        var result = JsonSerializer.Deserialize(value.GetRawText(), registration.TypeInfo);
        return result is TBaseType typedResult
            ? typedResult
            : throw new JsonException(
                $"Registered type '{registration.TypeInfo.Type}' is not assignable to '{typeof(TBaseType)}'.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        TBaseType value,
        JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        if (!_byType.TryGetValue(value.GetType(), out var registration))
        {
            throw new JsonException(
                $"Type '{value.GetType()}' is not registered for persistence as '{typeof(TBaseType)}'.");
        }

        writer.WriteStartObject();
        writer.WriteString(TypePropertyName, registration.Discriminator);
        writer.WritePropertyName(ValuePropertyName);
        JsonSerializer.Serialize(writer, value, registration.TypeInfo);
        writer.WriteEndObject();
    }
}

internal sealed class RegisteredEventJsonConverter : JsonConverter<IEvent>
{
    private const string TypePropertyName = "$type";
    private const string ValuePropertyName = "$value";
    private const string TriggerTimePropertyName = "TriggerTime";

    private readonly IReadOnlyDictionary<string, PersistenceEventRegistration> _byDiscriminator;
    private readonly IReadOnlyDictionary<Type, PersistenceEventRegistration> _byType;

    public RegisteredEventJsonConverter(IReadOnlyList<PersistenceEventRegistration> registrations)
    {
        _byDiscriminator = registrations.ToDictionary(
            registration => registration.Discriminator,
            StringComparer.Ordinal);
        _byType = registrations.ToDictionary(registration => registration.EventType);
    }

    public override IEvent Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (!root.TryGetProperty(TypePropertyName, out var typeProperty) ||
            typeProperty.ValueKind != JsonValueKind.String)
        {
            throw new JsonException($"Missing string discriminator '{TypePropertyName}'.");
        }

        var discriminator = typeProperty.GetString();
        if (discriminator is null ||
            !_byDiscriminator.TryGetValue(discriminator, out var registration))
        {
            throw new JsonException($"Unregistered persistence discriminator '{discriminator}'.");
        }

        if (!root.TryGetProperty(ValuePropertyName, out var value) ||
            !root.TryGetProperty(TriggerTimePropertyName, out var triggerTimeElement) ||
            !triggerTimeElement.TryGetUInt64(out var triggerTime))
        {
            throw new JsonException("Event state is missing a payload or valid trigger time.");
        }

        return registration.Read(value, triggerTime);
    }

    public override void Write(
        Utf8JsonWriter writer,
        IEvent value,
        JsonSerializerOptions options)
    {
        if (!_byType.TryGetValue(value.GetType(), out var registration))
        {
            throw new JsonException(
                $"Event type '{value.GetType()}' is not registered for persistence.");
        }

        registration.Write(writer, value);
    }
}