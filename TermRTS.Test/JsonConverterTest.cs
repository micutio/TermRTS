// See also: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism

namespace TermRTS.Test;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

public interface IAnimal
{
    string Name { get; set; }
    string MakeSound();
}

public class Dog : IAnimal
{
    public string Name { get; set; }
    public string Breed { get; set; }

    public string MakeSound()
    {
        return "Woof!";
    }

    public Dog()
    {
    } // Parameterless constructor
}

public class Cat : IAnimal
{
    public string Name { get; set; }
    public string Color { get; set; }

    public string MakeSound()
    {
        return "Meow!";
    }

    public Cat()
    {
    } // Parameterless constructor
}

public class AnimalTypeRegistry
{
    private readonly Dictionary<string, Type> _typeMap = new();

    public AnimalTypeRegistry()
    {
        RegisterTypesFromAssembly(Assembly.GetExecutingAssembly()); // Automatically register from this assembly
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

    public void RegisterTypesFromAssembly(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
            if (typeof(IAnimal).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                _typeMap[type.Name] = type;
    }
}

public class AnimalConverter(AnimalTypeRegistry registry) : JsonConverter<IAnimal>
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeof(IAnimal).IsAssignableFrom(typeToConvert);
    }

    public override IAnimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();

        using (var document = JsonDocument.ParseValue(ref reader))
        {
            if (!document.RootElement.TryGetProperty("Type", out var typeProperty))
                throw new JsonException("Type property is missing.");

            var typeName = typeProperty.GetString();
            var type = registry.GetType(typeName);

            if (type == null) throw new JsonException($"Unknown type: {typeName}");

            return (IAnimal)JsonSerializer.Deserialize(document.RootElement.ToString(), type, options);
        }
    }

    public override void Write(Utf8JsonWriter writer, IAnimal value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Type", value.GetType().Name);
        writer.WriteString("Name", value.Name);

        if (value is Dog dog)
            writer.WriteString("Breed", dog.Breed);
        else if (value is Cat cat) writer.WriteString("Color", cat.Color);

        writer.WriteEndObject();
    }
}

public class JsonConverterTest
{
    public static void TestConvertJson(string[] args)
    {
        var registry = new AnimalTypeRegistry();
        var options = new JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new AnimalConverter(registry));

        var animals = new List<IAnimal>
        {
            new Dog { Name = "Buddy", Breed = "Golden Retriever" },
            new Cat { Name = "Whiskers", Color = "Gray" }
        };

        var json = JsonSerializer.Serialize(animals, options);
        Console.WriteLine("Serialized JSON:\n" + json);

        var deserializedAnimals = JsonSerializer.Deserialize<List<IAnimal>>(json, options);

        Console.WriteLine("\nDeserialized Animals:");
        if (deserializedAnimals != null)
            foreach (var animal in deserializedAnimals)
            {
                Console.WriteLine($"{animal.Name} says {animal.MakeSound()} ({animal.GetType().Name})");
                if (animal is Dog dog)
                    Console.WriteLine($"  Breed: {dog.Breed}");
                else if (animal is Cat cat) Console.WriteLine($"  Color: {cat.Color}");
            }

        Assert.NotNull(deserializedAnimals);
        Assert.Equal(2, deserializedAnimals.Count);
        Assert.Equal(typeof(Dog), deserializedAnimals[0].GetType());
        Assert.Equal(typeof(Cat), deserializedAnimals[1].GetType());
    }
}