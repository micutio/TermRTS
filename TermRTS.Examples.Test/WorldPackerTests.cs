using System;
using TermRTS.Examples.Greenery.WorldGen;
using Xunit;

namespace TermRTS.Examples.Test;

public class WorldPackerTests
{
    [Fact]
    public void Pack_Unpack_RetainsAllFields()
    {
        var length = 64;
        var biomes = new Biome[length];
        var elevations = new byte[length];
        var humidities = new byte[length];
        var temperatures = new float[length];
        var waterflows = new (int x, int y)[length];
        var winds = new (int x, int y)[length];
        var features = new SurfaceFeature[length];
        var windSpeeds = new byte[length];

        var random = new Random(1234);
        for (var i = 0; i < length; i++)
        {
            biomes[i] = (Biome)random.Next(Enum.GetValues<Biome>().Length);
            elevations[i] = (byte)random.Next(0, 255);
            humidities[i] = (byte)random.Next(0, 100);
            temperatures[i] = random.Next(-40, 40);
            waterflows[i] = (
                random.Next(-1, 2),
                random.Next(-1, 2));
            winds[i] = (
                random.Next(-1, 2),
                random.Next(-1, 2));
            features[i] = (SurfaceFeature)random.Next(Enum.GetValues<SurfaceFeature>().Length);
            windSpeeds[i] = (byte)random.Next(0, 256);
        }

        var packed = WorldPacker.Pack(
            biomes,
            elevations,
            temperatures,
            humidities,
            waterflows,
            winds,
            windSpeeds,
            features);

        Assert.Equal(length, packed.Length);

        for (var i = 0; i < length; i++)
        {
            WorldPacker.Unpack(
                packed[i],
                out var biome,
                out var elevation,
                out var humidity,
                out var temperature,
                out var waterflow,
                out var wind,
                out var windSpeed,
                out var feature);

            Assert.Equal(biomes[i], biome);
            Assert.Equal(elevations[i], elevation);
            Assert.Equal(humidities[i], humidity);
            Assert.Equal(features[i], feature);
            Assert.Equal(Convert.ToSByte(temperatures[i]), temperature);
            Assert.Equal(waterflows[i], waterflow);
            Assert.Equal(winds[i], wind);
            Assert.Equal(windSpeeds[i], windSpeed);
        }
    }

    [Fact]
    public void Pack_BitsArePackedCorrectly_ForEdgeValues()
    {
        var biomes = new Biome[] { Biome.HighSeas, Biome.MajorRiver };
        var elevations = new byte[] { 0, 255 };
        var temperatures = new float[] { -40, 127 };
        var humidities = new byte[] { 0, 100 };
        var waterflows = new[] { (x: -1, y: -1), (x: 1, y: 1) };
        var winds = new[] { (x: -1, y: 1), (x: 1, y: -1) };
        var features = new[] { SurfaceFeature.None, SurfaceFeature.Snow };
        var windSpeeds = new byte[] { 0, 255 };

        var packed = WorldPacker.Pack(
            biomes,
            elevations,
            temperatures,
            humidities,
            waterflows,
            winds,
            windSpeeds,
            features);

        Assert.Equal((byte)0, packed[0].PackedVectors);
        Assert.Equal((byte)255, packed[1].PackedVectors);

        Assert.Equal(Biome.HighSeas, packed[0].Biome);
        Assert.Equal(Biome.MajorRiver, packed[1].Biome);
        Assert.Equal(0, packed[0].Elevation);
        Assert.Equal(255, packed[1].Elevation);
        Assert.Equal(SurfaceFeature.None, packed[0].SurfaceFeature);
        Assert.Equal(SurfaceFeature.Snow, packed[1].SurfaceFeature);

        Assert.Equal((-1, -1), packed[0].WaterFlow);
        Assert.Equal((1, 1), packed[1].WaterFlow);
        Assert.Equal((-1, 1), packed[0].Wind);
        Assert.Equal((1, -1), packed[1].Wind);
        Assert.Equal((byte)0, packed[0].Reserved);
        Assert.Equal((byte)255, packed[1].Reserved);
    }

    [Fact]
    public void Pack_ThrowsWhenArraysDifferInLength()
    {
        var biomes = new Biome[4];
        var elevations = new byte[4];
        var humidities = new byte[3];
        var temperatures = new float[4];
        var waterflows = new (int x, int y)[4];
        var winds = new (int x, int y)[4];
        var features = new SurfaceFeature[4];
        var windSpeeds = new byte[4];

        Assert.Throws<ArgumentException>(() => WorldPacker.Pack(
            biomes,
            elevations,
            temperatures,
            humidities,
            waterflows,
            winds,
            windSpeeds,
            features));
    }

    [Fact]
    public void Pack_ThrowsWhenDirectionsAreOutOfRange()
    {
        var biomes = new Biome[2];
        var elevations = new byte[2];
        var humidities = new byte[2];
        var temperatures = new float[2];
        var waterflows = new[] { (x: -2, y: 0), (x: 0, y: 0) };
        var winds = new[] { (x: 0, y: 0), (x: 0, y: 0) };
        var features = new SurfaceFeature[2];
        var windSpeeds = new byte[2];

        Assert.Throws<ArgumentOutOfRangeException>(() => WorldPacker.Pack(
            biomes,
            elevations,
            temperatures,
            humidities,
            waterflows,
            winds,
            windSpeeds,
            features));
    }

    [Fact]
    public void WorldGenerator_ProducesPackedChunksCompatibleWithEcs()
    {
        var worldGen = new CylinderWorld(
            WorldMath.WorldWidth,
            WorldMath.WorldHeight,
            0.35f,
            seed: 3,
            voronoiCellCount: 175,
            plateCount: 28,
            new ElevationParameters(),
            new CoastalParameters(),
            new VolcanicParameters(),
            new ErosionParameters(),
            new ClimateParameters(),
            new RiverParameters());

        var result = worldGen.Generate();

        var expectedChunkCount = WorldMath.ChunksAcross * (WorldMath.WorldHeight / WorldMath.ChunkSize);
        Assert.Equal(expectedChunkCount, result.PackedData.Length);

        foreach (var chunk in result.PackedData)
        {
            Assert.Equal(WorldMath.ChunkSize * WorldMath.ChunkSize, chunk.PackedTiles.Length);
            Assert.InRange(chunk.Cx, 0, WorldMath.ChunksAcross - 1);
            Assert.InRange(chunk.Cy, 0, WorldMath.WorldHeight / WorldMath.ChunkSize - 1);
            Assert.True(chunk.PackedTiles[0].Elevation >= 0);
        }
    }

    [Fact]
    public void PackPooled_RentAndReturn_Works()
    {
        var length = 32;
        var biomes = new Biome[length];
        var elevations = new byte[length];
        var humidities = new byte[length];
        var temperatures = new float[length];
        var waterflows = new (int x, int y)[length];
        var winds = new (int x, int y)[length];
        var features = new SurfaceFeature[length];
        var windSpeeds = new byte[length];

        for (var i = 0; i < length; i++)
        {
            biomes[i] = (Biome)(i % Enum.GetValues<Biome>().Length);
            elevations[i] = (byte)(i % 256);
            humidities[i] = (byte)(i % 101);
            temperatures[i] = i % 120 - 40;
            waterflows[i] = (0, 0);
            winds[i] = (0, 0);
            features[i] = SurfaceFeature.None;
            windSpeeds[i] = (byte)(i % 256);
        }

        var buffer = WorldPacker.PackPooled(
            biomes,
            elevations,
            temperatures,
            humidities,
            waterflows,
            winds,
            windSpeeds,
            features);

        Assert.NotNull(buffer);
        Assert.True(buffer.Length >= length);

        for (var i = 0; i < length; i++)
        {
            Assert.Equal(biomes[i], buffer[i].Biome);
            Assert.Equal(elevations[i], buffer[i].Elevation);
            Assert.Equal(humidities[i], buffer[i].Humidity);
        }

        // Return the pooled array to avoid leaking rented buffers in tests
        WorldPacker.ReturnPackedArray(buffer, clearArray: true);
    }
}
