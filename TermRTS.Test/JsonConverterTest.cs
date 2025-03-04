// See also: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism

using System.Text.Json;
using TermRTS.Serialization;
using Xunit.Abstractions;

namespace TermRTS.Test;

public interface IAnimal
{
    string Name { get; }
    string MakeSound();
}

public class Dog(string name, string breed) : IAnimal
{
    public string Name { get; set; } = name;
    public string Breed { get; init; } = breed;

    public string MakeSound()
    {
        return "Woof!";
    }
}

public class Cat(string name, string color) : IAnimal
{
    public string Name { get; set; } = name;
    public string Color { get; init; } = color;

    public string MakeSound()
    {
        return "Meow!";
    }
}

public class JsonConverterTest(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void TestConvertJson()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new BaseClassConverter<IAnimal>(typeof(Cat), typeof(Dog)));

        var animals = new List<IAnimal>
        {
            new Dog("Buddy", "Golden Retriever"),
            new Cat("Whiskers", "Gray")
        };

        var json = JsonSerializer.Serialize(animals, options);
        testOutputHelper.WriteLine("Serialized JSON:\n" + json);

        var deserializedAnimals = JsonSerializer.Deserialize<List<IAnimal>>(json, options);

        testOutputHelper.WriteLine("\nDeserialized Animals:");
        if (deserializedAnimals != null)
            foreach (var animal in deserializedAnimals)
            {
                testOutputHelper.WriteLine(
                    $"{animal.Name} says {animal.MakeSound()} ({animal.GetType().Name})");
                if (animal is Dog dog)
                    testOutputHelper.WriteLine($"  Breed: {dog.Breed}");
                else if (animal is Cat cat) testOutputHelper.WriteLine($"  Color: {cat.Color}");
            }

        Assert.NotNull(deserializedAnimals);
        Assert.Equal(2, deserializedAnimals.Count);
        Assert.Equal(typeof(Dog), deserializedAnimals[0].GetType());
        Assert.Equal(typeof(Cat), deserializedAnimals[1].GetType());
    }
}