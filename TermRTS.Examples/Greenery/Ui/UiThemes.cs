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
        (ConsoleColor.DarkBlue, ConsoleColor.Black),
        (ConsoleColor.Blue, ConsoleColor.Black),
        (ConsoleColor.Blue, ConsoleColor.DarkBlue),
        (ConsoleColor.Cyan, ConsoleColor.Blue),
        (ConsoleColor.Cyan, ConsoleColor.Yellow),
        (ConsoleColor.Green, ConsoleColor.DarkGreen),
        (ConsoleColor.Yellow, ConsoleColor.Green),
        (ConsoleColor.Green, ConsoleColor.DarkYellow),
        (ConsoleColor.DarkGray, ConsoleColor.Gray),
        (ConsoleColor.Gray, ConsoleColor.White)
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
    public Dictionary<SurfaceFeature, CellVisual> SurfaceFeatureMap { get; init; } = new()
    {
        // Water and Ice
        {
            SurfaceFeature.Ash,
            new CellVisual(Cp437.Solar, ConsoleColor.Gray, ConsoleColor.Black)
        },
        {
            SurfaceFeature.Beach,
            new CellVisual(Cp437.Interpunct, ConsoleColor.Yellow, ConsoleColor.DarkYellow)
        },
        {
            SurfaceFeature.Caldera,
            new CellVisual(Cp437.BulletHollowInverted, ConsoleColor.DarkYellow,
                ConsoleColor.DarkGreen)
        },
        {
            SurfaceFeature.Cinder,
            new CellVisual(Cp437.DoubleExclamation, ConsoleColor.Black, ConsoleColor.Gray)
        },
        {
            SurfaceFeature.Cliff,
            new CellVisual(Cp437.Pipe, ConsoleColor.Gray, ConsoleColor.Black)
        },
        {
            SurfaceFeature.Crater,
            new CellVisual(Cp437.BulletHollow, ConsoleColor.Red, ConsoleColor.Gray)
        },
        {
            SurfaceFeature.Fjord,
            new CellVisual(Cp437.HookedF, ConsoleColor.Green, ConsoleColor.Blue)
        },
        {
            SurfaceFeature.Glacier,
            new CellVisual(Cp437.Dollar, ConsoleColor.Cyan, ConsoleColor.White)
        },
        {
            SurfaceFeature.Lava,
            new CellVisual(Cp437.ArrowLeftRight, ConsoleColor.Red, ConsoleColor.DarkYellow)
        },
        {
            SurfaceFeature.Mountain,
            new CellVisual(Cp437.TriangleUp, ConsoleColor.Gray, ConsoleColor.Green)
        },
        {
            SurfaceFeature.None,
            new CellVisual(Cp437.WhiteSpace, ConsoleColor.Black, ConsoleColor.Black)
        },
        {
            SurfaceFeature.River,
            new CellVisual(Cp437.UnderScore, ConsoleColor.Cyan, ConsoleColor.Blue)
        },
        {
            SurfaceFeature.Shield,
            new CellVisual(Cp437.LowerZ, ConsoleColor.DarkYellow, ConsoleColor.Gray)
        },
        {
            SurfaceFeature.Snow,
            new CellVisual(Cp437.Asterisk, ConsoleColor.White, ConsoleColor.Gray)
        },
        {
            SurfaceFeature.Stratovolcano,
            new CellVisual(Cp437.TriangleUp, ConsoleColor.Red, ConsoleColor.Green)
        }
    };
}

public class BiomeTheme
{
    public Dictionary<Biome, CellVisual> BiomeMap { get; init; } = new()
    {
        // Water and Ice
        {
            Biome.HighSeas,
            new CellVisual(Cp437.Approximation, ConsoleColor.Black, ConsoleColor.DarkBlue)
        },
        {
            Biome.Ocean,
            new CellVisual(Cp437.Approximation, ConsoleColor.Blue, ConsoleColor.DarkBlue)
        },
        {
            Biome.Shelf,
            new CellVisual(Cp437.Tilde, ConsoleColor.DarkBlue, ConsoleColor.Blue)
        },
        {
            Biome.Shallows,
            new CellVisual(Cp437.Tilde, ConsoleColor.Cyan, ConsoleColor.Blue)
        },
        { Biome.IceCap, new CellVisual(Cp437.Asterisk, ConsoleColor.Gray, ConsoleColor.White) },
        {
            Biome.PolarDesert,
            new CellVisual(Cp437.Interpunct, ConsoleColor.DarkGray, ConsoleColor.White)
        },
        { Biome.Glacier, new CellVisual(Cp437.Dollar, ConsoleColor.Cyan, ConsoleColor.White) },
        // Frost
        { Biome.RockPeak, new CellVisual(Cp437.Caret, ConsoleColor.Gray, ConsoleColor.DarkGray) },
        {
            Biome.AlpineTundra,
            new CellVisual(Cp437.Dot, ConsoleColor.Gray, ConsoleColor.DarkGray)
        },
        {
            Biome.Tundra,
            new CellVisual(Cp437.TriangleLeft, ConsoleColor.DarkYellow, ConsoleColor.Gray)
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
            new CellVisual(Cp437.BulletHollow, ConsoleColor.Yellow, ConsoleColor.DarkYellow)
        },
        {
            Biome.HighlandMoor,
            new CellVisual(Cp437.TripleBar, ConsoleColor.DarkBlue, ConsoleColor.DarkGreen)
        },
        {
            Biome.Steppe,
            new CellVisual(Cp437.Comma, ConsoleColor.DarkGreen, ConsoleColor.DarkYellow)
        },
        {
            Biome.Grassland,
            new CellVisual(Cp437.LowerV, ConsoleColor.DarkGreen, ConsoleColor.Green)
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
            new CellVisual(Cp437.Solar, ConsoleColor.Red, ConsoleColor.Yellow)
        },
        { Biome.Savanna, new CellVisual(Cp437.Mu, ConsoleColor.DarkGreen, ConsoleColor.Yellow) },
        {
            Biome.TropicalSeasonalForest,
            new CellVisual(Cp437.DeckClub, ConsoleColor.DarkGreen, ConsoleColor.Green)
        },
        {
            Biome.TropicalRainforest,
            new CellVisual(Cp437.PhiLower, ConsoleColor.Green, ConsoleColor.DarkGreen)
        },
        // Rivers
        {
            Biome.Creek,
            new CellVisual(Cp437.Minus, ConsoleColor.Gray, ConsoleColor.Blue)
        },
        {
            Biome.MinorRiver,
            new CellVisual(Cp437.Equal, ConsoleColor.Cyan, ConsoleColor.Blue)
        },
        {
            Biome.MajorRiver,
            new CellVisual(Cp437.TripleBar, ConsoleColor.White, ConsoleColor.Blue)
        }
    };
}

public class ScalarTheme
{
    public char[] MarkersScalar { get; init; } =
    [
        Cp437.DenseShade,
        Cp437.DenseShade,
        Cp437.DenseShade,
        Cp437.DenseShade,
        Cp437.DenseShade,
        Cp437.DenseShade,
        Cp437.DenseShade,
        Cp437.DenseShade,
        Cp437.DenseShade,
        Cp437.DenseShade
    ];
}

public class DefaultTheme
{
    public ConsoleColor DefaultFg { get; init; } = ConsoleColor.Gray;
    public ConsoleColor DefaultBg { get; init; } = ConsoleColor.Black;
    public ConsoleColor RiverFg { get; init; } = ConsoleColor.Cyan;
}