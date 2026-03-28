using TermRTS.Examples.Greenery.WorldGen;

namespace TermRTS.Test;

public class GreeneryWorldGeneratorTest
{
    [Fact]
    public void Generate_WithValidParameters_ReturnsPopulatedWorld()
    {
        var generator = new CylinderWorld(40, 20, 1234, 10, 3);
        var result = generator.Generate(40, 20, 0.3f);

        Assert.NotNull(result);
        Assert.Equal(40, result.Elevation.GetLength(0));
        Assert.Equal(20, result.Elevation.GetLength(1));
        Assert.Equal(40, result.Surface.GetLength(0));
        Assert.Equal(20, result.Surface.GetLength(1));
        Assert.Equal(40, result.Temperature.GetLength(0));
        Assert.Equal(20, result.Temperature.GetLength(1));
        Assert.Equal(40, result.Humidity.GetLength(0));
        Assert.Equal(20, result.Humidity.GetLength(1));
        Assert.Equal(40, result.Biomes.GetLength(0));
        Assert.Equal(20, result.Biomes.GetLength(1));
        Assert.Equal(40, result.TemperatureAmplitude.GetLength(0));
        Assert.Equal(20, result.TemperatureAmplitude.GetLength(1));

        var hasLand = false;
        for (var x = 0; x < 40; x++)
        {
            for (var y = 0; y < 20; y++)
            {
                Assert.InRange(result.Elevation[x, y], (byte)0, (byte)9);
                Assert.InRange(result.Humidity[x, y], 0f, 1f);
                Assert.False(float.IsNaN(result.Temperature[x, y]));
                Assert.False(float.IsNaN(result.TemperatureAmplitude[x, y]));
                if (result.Biomes[x, y] != Biome.Ocean)
                    hasLand = true;
            }
        }

        Assert.True(hasLand, "Expected at least one land biome in the generated world.");
    }

    [Fact]
    public void Generate_SameSeedProducesSameWorld()
    {
        var generatorA = new CylinderWorld(40, 20, 1234, 10, 3);
        var generatorB = new CylinderWorld(40, 20, 1234, 10, 3);

        var resultA = generatorA.Generate(40, 20, 0.3f);
        var resultB = generatorB.Generate(40, 20, 0.3f);

        Assert2DArrayEqual(resultA.Elevation, resultB.Elevation);
        Assert2DArrayEqual(resultA.Surface, resultB.Surface);
        Assert2DArrayEqual(resultA.Temperature, resultB.Temperature);
        Assert2DArrayEqual(resultA.Humidity, resultB.Humidity);
        Assert2DArrayEqual(resultA.Biomes, resultB.Biomes);
        Assert2DArrayEqual(resultA.TemperatureAmplitude, resultB.TemperatureAmplitude);
    }

    [Theory]
    [InlineData(0, 10, 0.3f, "worldWidth")]
    [InlineData(10, 0, 0.3f, "worldHeight")]
    [InlineData(10, 10, -0.1f, "landRatio")]
    [InlineData(10, 10, 1.1f, "landRatio")]
    public void Generate_InvalidParameters_ThrowsArgumentOutOfRangeException(
        int worldWidth,
        int worldHeight,
        float landRatio,
        string paramName)
    {
        var generator = new CylinderWorld(10, 10, 1234, 4, 2);
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => generator.Generate(worldWidth, worldHeight, landRatio));

        Assert.Equal(paramName, exception.ParamName);
    }

    private static void Assert2DArrayEqual<T>(T[,] expected, T[,] actual)
    {
        Assert.Equal(expected.GetLength(0), actual.GetLength(0));
        Assert.Equal(expected.GetLength(1), actual.GetLength(1));

        for (var x = 0; x < expected.GetLength(0); x++)
        for (var y = 0; y < expected.GetLength(1); y++)
            Assert.Equal(expected[x, y], actual[x, y]);
    }
}
