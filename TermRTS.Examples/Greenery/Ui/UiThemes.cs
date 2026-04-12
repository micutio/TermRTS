using TermRTS.Examples.Greenery.WorldGen;
using TermRTS.Io;

namespace TermRTS.Examples.Greenery.Ui;

public class UiThemes
{
    public ElevationTheme Elevation { get; init; } = new();
    public HeatmapTheme Heatmap { get; init; } = new();
    public SurfaceFeatureTheme SurfaceFeature { get; init; } = new();
    public BiomeTheme Biome { get; init; } = new();
    public ScalarTheme Scalar { get; init; } = new();
    public DefaultTheme Default { get; init; } = new();
}

public class ElevationTheme
{
    public char[] MarkersElevation { get; init; } =
        ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];

    public (ConsoleColor, ConsoleColor)[] ColorsElevation { get; init; } =
    [
        (ConsoleColor.Black, ConsoleColor.Black),
        (ConsoleColor.DarkBlue, ConsoleColor.Black),
        (ConsoleColor.Blue, ConsoleColor.DarkBlue),
        (ConsoleColor.Cyan, ConsoleColor.Blue),
        (ConsoleColor.Green, ConsoleColor.Cyan),
        (ConsoleColor.Yellow, ConsoleColor.Green),
        (ConsoleColor.DarkYellow, ConsoleColor.Yellow),
        (ConsoleColor.Red, ConsoleColor.DarkYellow),
        (ConsoleColor.DarkRed, ConsoleColor.Red),
        (ConsoleColor.White, ConsoleColor.DarkRed)
    ];

    public (ConsoleColor, ConsoleColor)[] ColorsElevationMonochrome { get; init; } =
    [
        (ConsoleColor.Gray, ConsoleColor.Black),
        (ConsoleColor.Gray, ConsoleColor.Black),
        (ConsoleColor.Gray, ConsoleColor.Black),
        (ConsoleColor.Gray, ConsoleColor.Black),
        (ConsoleColor.Gray, ConsoleColor.Black),
        (ConsoleColor.Gray, ConsoleColor.Black),
        (ConsoleColor.Gray, ConsoleColor.Black),
        (ConsoleColor.Gray, ConsoleColor.Black),
        (ConsoleColor.Gray, ConsoleColor.Black),
        (ConsoleColor.Gray, ConsoleColor.Black)
    ];
}

public class HeatmapTheme
{
    public char[] MarkersHeatmapMonochrome { get; init; } =
        ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];

    public (ConsoleColor, ConsoleColor)[] ColorsHeatmapTemperature { get; init; } =
    [
        // From cold to warm.
        (ConsoleColor.DarkBlue, ConsoleColor.Black),
        (ConsoleColor.Blue, ConsoleColor.DarkBlue),
        (ConsoleColor.Blue, ConsoleColor.Cyan),
        (ConsoleColor.Green, ConsoleColor.DarkGreen),
        (ConsoleColor.DarkGreen, ConsoleColor.Green),
        (ConsoleColor.Green, ConsoleColor.Yellow),
        (ConsoleColor.Yellow, ConsoleColor.DarkYellow),
        (ConsoleColor.Yellow, ConsoleColor.Red),
        (ConsoleColor.Red, ConsoleColor.Yellow),
        (ConsoleColor.Red, ConsoleColor.Red)
    ];

    public (ConsoleColor, ConsoleColor)[] ColorsHeatmapHumidity { get; init; } =
    [
        // From dry to humid.
        (ConsoleColor.Red, ConsoleColor.Red),
        (ConsoleColor.Red, ConsoleColor.Yellow),
        (ConsoleColor.Yellow, ConsoleColor.Red),
        (ConsoleColor.Yellow, ConsoleColor.DarkYellow),
        (ConsoleColor.Green, ConsoleColor.Yellow),
        (ConsoleColor.DarkGreen, ConsoleColor.Green),
        (ConsoleColor.Green, ConsoleColor.DarkGreen),
        (ConsoleColor.Blue, ConsoleColor.Cyan),
        (ConsoleColor.Blue, ConsoleColor.DarkBlue),
        (ConsoleColor.DarkBlue, ConsoleColor.Black)
    ];
}

public class SurfaceFeatureTheme
{
    public CellVisual[] CellVisualSurfaceFeatures { get; init; } =
    [
        new(Cp437.WhiteSpace, ConsoleColor.Gray, ConsoleColor.Black), // None
        new(Cp437.Approximation, ConsoleColor.Blue, ConsoleColor.DarkBlue), // Ocean
        new(Cp437.BlockFull, ConsoleColor.White, ConsoleColor.White), // IceCap
        new(Cp437.Minus, ConsoleColor.DarkGray, ConsoleColor.White), // PolarDesert
        new(Cp437.MediumShade, ConsoleColor.Cyan, ConsoleColor.White), // Glacier
        new(Cp437.Caret, ConsoleColor.Gray, ConsoleColor.DarkGray), // RockPeak
        new(Cp437.Minus, ConsoleColor.Gray, ConsoleColor.DarkGray), // AlpineTundra
        new(Cp437.BoxHorizontal, ConsoleColor.DarkYellow, ConsoleColor.Gray), // Tundra
        new(Cp437.ArrowUpDownWithBase, ConsoleColor.White, ConsoleColor.Gray), // SnowyForest
        new(Cp437.ArrowUp, ConsoleColor.DarkGreen, ConsoleColor.DarkYellow), // Taiga
        new(Cp437.MediumShade, ConsoleColor.Cyan, ConsoleColor.DarkYellow), // ColdDesert
        new(Cp437.TripleBar, ConsoleColor.DarkBlue, ConsoleColor.DarkGreen), // HighlandMoor
        new(Cp437.Rectangle, ConsoleColor.DarkGreen, ConsoleColor.DarkYellow), // Steppe
        new(Cp437.LowerV, ConsoleColor.Green, ConsoleColor.DarkGreen), // Grassland
        new(Cp437.DeckSpade, ConsoleColor.Yellow, ConsoleColor.Green), // TemperateForest
        new(Cp437.Beta, ConsoleColor.DarkCyan, ConsoleColor.Green), // CloudForest
        new(Cp437.SparseShade, ConsoleColor.Yellow, ConsoleColor.DarkYellow), // HotDesert
        new(Cp437.Mu, ConsoleColor.DarkGreen, ConsoleColor.Yellow), // Savanna
        new(Cp437.DeckClub, ConsoleColor.Green, ConsoleColor.DarkGreen), // TropicalSeasonalForest
        new(Cp437.PhiLower, ConsoleColor.Green, ConsoleColor.DarkGreen), // TropicalRainforest
        new(Cp437.Minus, ConsoleColor.Cyan, ConsoleColor.Blue), // Creek
        new(Cp437.Equal, ConsoleColor.Cyan, ConsoleColor.Blue), // MinorRiver
        new(Cp437.TripleBar, ConsoleColor.Cyan, ConsoleColor.Blue) // MajorRiver
    ];
}

public class BiomeTheme
{
    public Dictionary<Biome, CellVisual> BiomeMap { get; init; } = new()
    {
        // Water and Ice
        {
            Biome.Ocean,
            new CellVisual(Cp437.Approximation, ConsoleColor.Blue, ConsoleColor.DarkBlue)
        },
        { Biome.IceCap, new CellVisual(Cp437.BlockFull, ConsoleColor.White, ConsoleColor.White) },
        {
            Biome.PolarDesert,
            new CellVisual(Cp437.Minus, ConsoleColor.DarkGray, ConsoleColor.White)
        },
        { Biome.Glacier, new CellVisual(Cp437.MediumShade, ConsoleColor.Cyan, ConsoleColor.White) },
        // Frost
        { Biome.RockPeak, new CellVisual(Cp437.Caret, ConsoleColor.Gray, ConsoleColor.DarkGray) },
        {
            Biome.AlpineTundra,
            new CellVisual(Cp437.Minus, ConsoleColor.Gray, ConsoleColor.DarkGray)
        },
        {
            Biome.Tundra,
            new CellVisual(Cp437.BoxHorizontal, ConsoleColor.DarkYellow, ConsoleColor.Gray)
        },
        {
            Biome.SnowyForest,
            new CellVisual(Cp437.ArrowUpDownWithBase, ConsoleColor.White, ConsoleColor.Gray)
        },
        {
            Biome.Taiga,
            new CellVisual(Cp437.ArrowUp, ConsoleColor.DarkGreen, ConsoleColor.DarkYellow)
        },
        // Temperate
        {
            Biome.ColdDesert,
            new CellVisual(Cp437.MediumShade, ConsoleColor.Cyan, ConsoleColor.DarkYellow)
        },
        {
            Biome.HighlandMoor,
            new CellVisual(Cp437.TripleBar, ConsoleColor.DarkBlue, ConsoleColor.DarkGreen)
        },
        {
            Biome.Steppe,
            new CellVisual(Cp437.Rectangle, ConsoleColor.DarkGreen, ConsoleColor.DarkYellow)
        },
        {
            Biome.Grassland,
            new CellVisual(Cp437.LowerV, ConsoleColor.Green, ConsoleColor.DarkGreen)
        },
        {
            Biome.TemperateForest,
            new CellVisual(Cp437.DeckSpade, ConsoleColor.Yellow, ConsoleColor.Green)
        },
        // Tropical
        {
            Biome.CloudForest, new CellVisual(Cp437.Beta, ConsoleColor.DarkCyan, ConsoleColor.Green)
        },
        {
            Biome.HotDesert,
            new CellVisual(Cp437.SparseShade, ConsoleColor.Yellow, ConsoleColor.DarkYellow)
        },
        { Biome.Savanna, new CellVisual(Cp437.Mu, ConsoleColor.DarkGreen, ConsoleColor.Yellow) },
        {
            Biome.TropicalSeasonalForest,
            new CellVisual(Cp437.DeckClub, ConsoleColor.Green, ConsoleColor.DarkGreen)
        },
        {
            Biome.TropicalRainforest,
            new CellVisual(Cp437.PhiLower, ConsoleColor.Green, ConsoleColor.DarkGreen)
        },
        // Rivers
        {
            Biome.Creek,
            new CellVisual(Cp437.Minus, ConsoleColor.Cyan, ConsoleColor.Blue)
        },
        {
            Biome.MinorRiver,
            new CellVisual(Cp437.Equal, ConsoleColor.Cyan, ConsoleColor.Blue)
        },
        {
            Biome.MajorRiver,
            new CellVisual(Cp437.TripleBar, ConsoleColor.Cyan, ConsoleColor.Blue)
        }
    };
}

public class ScalarTheme
{
    public char[] MarkersScalar { get; init; } = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];
}

public class DefaultTheme
{
    public ConsoleColor DefaultFg { get; init; } = ConsoleColor.Gray;
    public ConsoleColor DefaultBg { get; init; } = ConsoleColor.Black;
    public ConsoleColor RiverFg { get; init; } = ConsoleColor.Cyan;
}