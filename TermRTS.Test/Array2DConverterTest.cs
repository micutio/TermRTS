using System.Text.Json;
using TermRTS.Serialization;

namespace TermRTS.Test;

public class Array2DConverterTest
{
    [Fact]
    public void TestConvertByteMatrix()
    {
        var bytes = new byte[,] { { 1, 2, 3 }, { 4, 5, 6 }, { 7, 8, 9 } };

        var options = new JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new ByteArray2DConverter());

        var jsonStr = JsonSerializer.Serialize(bytes, options);
        var deserializedBytes = JsonSerializer.Deserialize<byte[,]>(jsonStr, options);
        Assert.Equal(bytes, deserializedBytes);
    }

    [Fact]
    public void TestConvertBooleanMatrix()
    {
        var booleans = new[,] { { true, false, true }, { false, false, true }, { false, true, true } };

        var options = new JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new BooleanArray2DConverter());

        var jsonStr = JsonSerializer.Serialize(booleans, options);
        var deserializedBytes = JsonSerializer.Deserialize<bool[,]>(jsonStr, options);
        Assert.Equal(booleans, deserializedBytes);
    }
}