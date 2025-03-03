// See also: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism

using System.Text.Json.Nodes;
using TermRTS.Serialization;
using Xunit.Abstractions;

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

public class JsonConverterTest(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void TestConvertJson()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new BaseClassConverter<IAnimal>(typeof(Cat), typeof(Dog)));
        
        var animals = new List<IAnimal>
        {
            new Dog { Name = "Buddy", Breed = "Golden Retriever" },
            new Cat { Name = "Whiskers", Color = "Gray" }
        };
        
        var json = JsonSerializer.Serialize(animals, options);
        testOutputHelper.WriteLine("Serialized JSON:\n" + json);
        
        var deserializedAnimals = JsonSerializer.Deserialize<List<IAnimal>>(json, options);
        
        testOutputHelper.WriteLine("\nDeserialized Animals:");
        if (deserializedAnimals != null)
            foreach (var animal in deserializedAnimals)
            {
                testOutputHelper.WriteLine($"{animal.Name} says {animal.MakeSound()} ({animal.GetType().Name})");
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