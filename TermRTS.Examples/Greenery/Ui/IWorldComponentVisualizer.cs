using TermRTS.Examples.Greenery.Ecs.Component;
using TermRTS.Examples.Greenery.WorldGen;
using TermRTS.Io;
using TermRTS.Storage;

namespace TermRTS.Examples.Greenery.Ui;

internal static class Visual
{
    internal static readonly ConsoleColor DefaultFg = ConsoleColor.Gray;
    internal static readonly ConsoleColor DefaultBg = ConsoleColor.Black;

    internal static readonly char[] MarkersElevation =
        ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];

    internal static readonly (ConsoleColor, ConsoleColor)[] ColorsElevation =
    [
        (ConsoleColor.DarkBlue, DefaultBg),
        (ConsoleColor.Blue, DefaultBg),
        (ConsoleColor.DarkCyan, DefaultBg),
        (ConsoleColor.Cyan, DefaultBg),
        (ConsoleColor.Yellow, DefaultBg),
        (ConsoleColor.DarkGreen, DefaultBg),
        (ConsoleColor.Green, DefaultBg),
        (ConsoleColor.DarkYellow, DefaultBg),
        (ConsoleColor.DarkGray, DefaultBg),
        (ConsoleColor.Gray, DefaultBg)
    ];

    internal static readonly char[] MarkersTerrain =
    [
        Cp437.Tilde,
        Cp437.Tilde,
        Cp437.Approximation,
        Cp437.Approximation,
        Cp437.SparseShade,
        Cp437.BoxDoubleUpHorizontal,
        Cp437.BoxUpHorizontal,
        Cp437.Intersection,
        Cp437.Caret,
        Cp437.TriangleUp
    ];

    internal static readonly char[] MarkersHeatmapColor =
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

    internal static readonly char[] MarkersHeatmapMonochrome =
    [
        Cp437.BlockFull,
        Cp437.SparseShade,
        Cp437.MediumShade,
        Cp437.DenseShade,
        Cp437.SparseShade,
        Cp437.MediumShade,
        Cp437.DenseShade,
        Cp437.MediumShade,
        Cp437.DenseShade,
        Cp437.BlockFull
    ];

    internal static readonly CellVisual[] CellVisualSurfaceFeatures =
    [
        new(Cp437.WhiteSpace, DefaultFg, DefaultBg), // None
        new(Cp437.Tilde, ConsoleColor.Cyan, ConsoleColor.Blue), // River
        new(Cp437.MediumShade, ConsoleColor.White, DefaultBg), // Glacier
        new(Cp437.BlockFull, ConsoleColor.Red, ConsoleColor.DarkGray), // Lava
        new(Cp437.TriangleUp, ConsoleColor.Gray, DefaultBg), // Mountain
        new(Cp437.Solar, ConsoleColor.White, DefaultBg), // Snow
        new(Cp437.Dot, ConsoleColor.Yellow, ConsoleColor.DarkYellow), // Beach
        new(Cp437.BoxDoubleUpHorizontal, ConsoleColor.DarkGray, DefaultBg), // Cliff
        new(Cp437.Pipe, ConsoleColor.Gray, ConsoleColor.Blue), // Fjord
        new(Cp437.BulletHollow, ConsoleColor.DarkRed, ConsoleColor.DarkGray), // Crater
        new(Cp437.Percent, ConsoleColor.Gray, DefaultBg), // Ash
        new(Cp437.Asterisk, ConsoleColor.Gray, DefaultBg), // Cinder
        new(Cp437.Plus, ConsoleColor.DarkGreen, ConsoleColor.DarkGray), // Caldera
        new(Cp437.Rectangle, ConsoleColor.Black, ConsoleColor.Gray), // Shield
        new(Cp437.Intersection, ConsoleColor.DarkRed, ConsoleColor.DarkGray) // Stratovolcano
    ];

    internal static readonly char[] MarkersScalar =
    [
        Cp437.WhiteSpace,
        Cp437.Dot,
        Cp437.Comma,
        Cp437.SparseShade,
        Cp437.MediumShade,
        Cp437.DenseShade,
        Cp437.BlockFull,
        Cp437.Plus,
        Cp437.Hash,
        Cp437.TriangleUp
    ];

    internal static readonly (ConsoleColor, ConsoleColor)[] ColorsElevationMonochrome =
    [
        (DefaultFg, DefaultBg),
        (DefaultFg, DefaultBg),
        (DefaultFg, DefaultBg),
        (DefaultFg, DefaultBg),
        (DefaultFg, DefaultBg),
        (DefaultFg, DefaultBg),
        (DefaultFg, DefaultBg),
        (DefaultFg, DefaultBg),
        (DefaultFg, DefaultBg),
        (DefaultFg, DefaultBg)
    ];

    internal static readonly (ConsoleColor, ConsoleColor)[] ColorsHeatmapMonochrome =
    [
        (ConsoleColor.Black, ConsoleColor.Black),
        (ConsoleColor.DarkGray, ConsoleColor.Black),
        (ConsoleColor.DarkGray, ConsoleColor.Black),
        (ConsoleColor.DarkGray, ConsoleColor.Black),
        (ConsoleColor.Gray, ConsoleColor.DarkGray),
        (ConsoleColor.Gray, ConsoleColor.DarkGray),
        (ConsoleColor.Gray, ConsoleColor.DarkGray),
        (ConsoleColor.White, ConsoleColor.DarkGray),
        (ConsoleColor.White, ConsoleColor.DarkGray),
        (ConsoleColor.White, ConsoleColor.DarkGray)
    ];

    internal static readonly (ConsoleColor, ConsoleColor)[] ColorsHeatmapTemperature =
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

    internal static readonly (ConsoleColor, ConsoleColor)[] ColorsHeatmapHumidity =
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

    internal static readonly Dictionary<Biome, CellVisual> BiomeMap = new()
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

    internal static int GetScalarIndex(float value, float min, float max)
    {
        if (float.IsNaN(value)) return 0;
        if (max <= min) return 0;
        var normalized = (value - min) / (max - min);
        return Math.Clamp((int)MathF.Floor(normalized * 9.0f), 0, 9);
    }
}

internal interface IWorldComponentVisualizer
{
    void SetVisuals(
        in IReadonlyStorage storage,
        in CellVisual[] viewportBuffer, // Sized [viewportWidth * viewportHeight]
        int viewWorldX, // World X at top-left of screen
        int viewWorldY, // World Y at top-left of screen
        int viewportWidth,
        int viewportHeight
    );
}

internal class ElevationVisualizer(char[] markers, (ConsoleColor, ConsoleColor)[] colors)
    : IWorldComponentVisualizer
{
    public void SetVisuals(
        in IReadonlyStorage storage,
        in CellVisual[] viewportBuffer,
        int viewWorldX,
        int viewWorldY,
        int viewportWidth,
        int viewportHeight
    )
    {
        for (var vY = 0; vY < viewportHeight; vY++)
        {
            var worldY = viewWorldY + vY;

            // 1. Vertical Clamping (The Poles)
            // If the camera goes off the top/bottom, we draw nothing or "Void"
            if (worldY is < 0 or >= WorldMath.WorldHeight) continue;

            WorldElevationChunk? currentChunk = null;
            var lastCx = -1;
            var lastCy = -1;

            for (var vX = 0; vX < viewportWidth; vX++)
            {
                // 2. Horizontal Wrapping (The Cylinder)
                // This handles negative viewWorldX (scrolling left) and > Width (scrolling right)
                var worldX = WorldMath.WrapX(viewWorldX + vX);

                // 3. Get Chunk and Local Coords
                var (cx, cy, lx, ly) = WorldMath.ToRelative(worldX, worldY);
                var chunkIdx = cy * WorldMath.ChunksAcross + cx;

                if (cx != lastCx || cy != lastCy)
                {
                    if (!storage.TryGetSingleForTypeAndEntity<WorldElevationChunk>(chunkIdx,
                            out var chunk) || chunk == null) continue;

                    currentChunk = chunk;
                    lastCx = cx;
                    lastCy = cy;
                }

                if (currentChunk == null) continue;

                // 4. Extract data from the 32x32 slab
                var elevation = currentChunk.Elevation[(ly << 5) + lx];

                // 5. Map to your TUI visuals
                var marker = markers[elevation];
                var (fg, bg) = colors[elevation];

                // 6. Write to the VIEWPORT-relative buffer
                viewportBuffer[vY * viewportWidth + vX] = new CellVisual(marker, fg, bg);
            }
        }
    }
}

internal class ElevationHeatmapVisualizer(char[] markers, (ConsoleColor, ConsoleColor)[] colors)
    : IWorldComponentVisualizer
{
    public void SetVisuals(
        in IReadonlyStorage storage,
        in CellVisual[] viewportBuffer,
        int viewWorldX,
        int viewWorldY,
        int viewportWidth,
        int viewportHeight
    )
    {
        for (var vY = 0; vY < viewportHeight; vY++)
        {
            var worldY = viewWorldY + vY;

            // 1. Vertical Clamping (The Poles)
            // If the camera goes off the top/bottom, we draw nothing or "Void"
            if (worldY is < 0 or >= WorldMath.WorldHeight) continue;

            WorldElevationChunk? currentChunk = null;
            var lastCx = -1;
            var lastCy = -1;

            for (var vX = 0; vX < viewportWidth; vX++)
            {
                // 2. Horizontal Wrapping (The Cylinder)
                // This handles negative viewWorldX (scrolling left) and > Width (scrolling right)
                var worldX = WorldMath.WrapX(viewWorldX + vX);

                // 3. Get Chunk and Local Coords
                var (cx, cy, lx, ly) = WorldMath.ToRelative(worldX, worldY);
                var chunkIdx = cy * WorldMath.ChunksAcross + cx;

                if (cx != lastCx || cy != lastCy)
                {
                    if (!storage.TryGetSingleForTypeAndEntity<WorldElevationChunk>(chunkIdx,
                            out var chunk) || chunk == null) continue;

                    currentChunk = chunk;
                    lastCx = cx;
                    lastCy = cy;
                }

                if (currentChunk == null) continue;

                // 4. Extract data from the 32x32 slab
                var elevation = currentChunk.Elevation[(ly << 5) + lx];
                var index = Visual.GetScalarIndex(elevation, 0, 9);

                // 5. Map to your TUI visuals
                var marker = markers[index];
                var (fg, bg) = colors[index];

                // 6. Write to the VIEWPORT-relative buffer
                viewportBuffer[vY * viewportWidth + vX] = new CellVisual(marker, fg, bg);
            }
        }
    }
}

internal class SurfaceFeatureVisualizer(Dictionary<SurfaceFeature, CellVisual> surfaceFeatureMap)
    : IWorldComponentVisualizer
{
    public void SetVisuals(
        in IReadonlyStorage storage,
        in CellVisual[] viewportBuffer,
        int viewWorldX,
        int viewWorldY,
        int viewportWidth,
        int viewportHeight
    )
    {
        for (var vY = 0; vY < viewportHeight; vY++)
        {
            var worldY = viewWorldY + vY;

            // 1. Vertical Clamping (The Poles)
            // If the camera goes off the top/bottom, we draw nothing or "Void"
            if (worldY is < 0 or >= WorldMath.WorldHeight) continue;

            WorldSurfaceFeatureChunk? currentChunk = null;
            var lastCx = -1;
            var lastCy = -1;

            for (var vX = 0; vX < viewportWidth; vX++)
            {
                // 2. Horizontal Wrapping (The Cylinder)
                // This handles negative viewWorldX (scrolling left) and > Width (scrolling right)
                var worldX = WorldMath.WrapX(viewWorldX + vX);

                // 3. Get Chunk and Local Coords
                var (cx, cy, lx, ly) = WorldMath.ToRelative(worldX, worldY);
                var chunkIdx = cy * WorldMath.ChunksAcross + cx;

                if (cx != lastCx || cy != lastCy)
                {
                    if (!storage.TryGetSingleForTypeAndEntity<WorldSurfaceFeatureChunk>(chunkIdx,
                            out var chunk) || chunk == null) continue;

                    currentChunk = chunk;
                    lastCx = cx;
                    lastCy = cy;
                }

                if (currentChunk == null) continue;

                // 4. Extract data from the 32x32 slab
                var feature = currentChunk.SurfaceFeature[(ly << 5) + lx];

                // 5. Map to your TUI visuals
                if (surfaceFeatureMap.TryGetValue(feature, out var marker))
                {
                    // 6. Write to the VIEWPORT-relative buffer
                    viewportBuffer[vY * viewportWidth + vX] = marker;
                }
            }
        }
    }
}

internal class TemperatureVisualizer(char[] markers, (ConsoleColor, ConsoleColor)[] colors)
    : IWorldComponentVisualizer
{
    private readonly char[] _markers = markers;
    private readonly (ConsoleColor, ConsoleColor)[] _colors = colors;

    public void SetVisuals(
        in IReadonlyStorage storage,
        in CellVisual[] viewportBuffer,
        int viewWorldX,
        int viewWorldY,
        int viewportWidth,
        int viewportHeight
    )
    {
        for (var vY = 0; vY < viewportHeight; vY++)
        {
            var worldY = viewWorldY + vY;

            // 1. Vertical Clamping (The Poles)
            // If the camera goes off the top/bottom, we draw nothing or "Void"
            if (worldY is < 0 or >= WorldMath.WorldHeight) continue;

            WorldTemperatureChunk? currentChunk = null;
            var lastCx = -1;
            var lastCy = -1;

            for (var vX = 0; vX < viewportWidth; vX++)
            {
                // 2. Horizontal Wrapping (The Cylinder)
                // This handles negative viewWorldX (scrolling left) and > Width (scrolling right)
                var worldX = WorldMath.WrapX(viewWorldX + vX);

                // 3. Get Chunk and Local Coords
                var (cx, cy, lx, ly) = WorldMath.ToRelative(worldX, worldY);
                var chunkIdx = cy * WorldMath.ChunksAcross + cx;

                if (cx != lastCx || cy != lastCy)
                {
                    if (!storage.TryGetSingleForTypeAndEntity<WorldTemperatureChunk>(chunkIdx,
                            out var chunk) || chunk == null) continue;

                    currentChunk = chunk;
                    lastCx = cx;
                    lastCy = cy;
                }

                if (currentChunk == null) continue;

                // 4. Extract data from the 32x32 slab
                var temperature = currentChunk.Temperature[(ly << 5) + lx];
                // TODO: Tie to world gen constants.
                var index = Visual.GetScalarIndex(temperature, -50, 35);

                // 5. Map to your TUI visuals
                var marker = _markers[index];
                var (fg, bg) = _colors[index];

                // 6. Write to the VIEWPORT-relative buffer
                viewportBuffer[vY * viewportWidth + vX] = new CellVisual(marker, fg, bg);
            }
        }
    }
}

internal class HumidityVisualizer(char[] markers, (ConsoleColor, ConsoleColor)[] colors)
    : IWorldComponentVisualizer
{
    private readonly char[] _markers = markers;
    private readonly (ConsoleColor, ConsoleColor)[] _colors = colors;

    public void SetVisuals(
        in IReadonlyStorage storage,
        in CellVisual[] viewportBuffer,
        int viewWorldX,
        int viewWorldY,
        int viewportWidth,
        int viewportHeight
    )
    {
        for (var vY = 0; vY < viewportHeight; vY++)
        {
            var worldY = viewWorldY + vY;

            // 1. Vertical Clamping (The Poles)
            // If the camera goes off the top/bottom, we draw nothing or "Void"
            if (worldY is < 0 or >= WorldMath.WorldHeight) continue;

            WorldHumidityChunk? currentChunk = null;
            var lastCx = -1;
            var lastCy = -1;

            for (var vX = 0; vX < viewportWidth; vX++)
            {
                // 2. Horizontal Wrapping (The Cylinder)
                // This handles negative viewWorldX (scrolling left) and > Width (scrolling right)
                var worldX = WorldMath.WrapX(viewWorldX + vX);

                // 3. Get Chunk and Local Coords
                var (cx, cy, lx, ly) = WorldMath.ToRelative(worldX, worldY);
                var chunkIdx = cy * WorldMath.ChunksAcross + cx;

                if (cx != lastCx || cy != lastCy)
                {
                    if (!storage.TryGetSingleForTypeAndEntity<WorldHumidityChunk>(chunkIdx,
                            out var chunk) || chunk == null) continue;

                    currentChunk = chunk;
                    lastCx = cx;
                    lastCy = cy;
                }

                if (currentChunk == null) continue;

                // 4. Extract data from the 32x32 slab
                var humidity = currentChunk.Humidity[(ly << 5) + lx];
                var index = Convert.ToInt32(9 * humidity);
                // var index = Visual.GetScalarIndex(humidity, 0, 9);

                // 5. Map to your TUI visuals
                var marker = _markers[index];
                var (fg, bg) = _colors[index];

                // 6. Write to the VIEWPORT-relative buffer
                viewportBuffer[vY * viewportWidth + vX] = new CellVisual(marker, fg, bg);
            }
        }
    }
}

internal class TemperatureAmplitudeVisualizer(char[] markers, (ConsoleColor, ConsoleColor)[] colors)
    : IWorldComponentVisualizer
{
    private readonly char[] _markers = markers;
    private readonly (ConsoleColor, ConsoleColor)[] _colors = colors;

    public void SetVisuals(
        in IReadonlyStorage storage,
        in CellVisual[] viewportBuffer,
        int viewWorldX,
        int viewWorldY,
        int viewportWidth,
        int viewportHeight
    )
    {
        for (var vY = 0; vY < viewportHeight; vY++)
        {
            var worldY = viewWorldY + vY;

            // 1. Vertical Clamping (The Poles)
            // If the camera goes off the top/bottom, we draw nothing or "Void"
            if (worldY is < 0 or >= WorldMath.WorldHeight) continue;

            WorldTemperatureAmplitudeChunk? currentChunk = null;
            var lastCx = -1;
            var lastCy = -1;

            for (var vX = 0; vX < viewportWidth; vX++)
            {
                // 2. Horizontal Wrapping (The Cylinder)
                // This handles negative viewWorldX (scrolling left) and > Width (scrolling right)
                var worldX = WorldMath.WrapX(viewWorldX + vX);

                // 3. Get Chunk and Local Coords
                var (cx, cy, lx, ly) = WorldMath.ToRelative(worldX, worldY);
                var chunkIdx = cy * WorldMath.ChunksAcross + cx;

                if (cx != lastCx || cy != lastCy)
                {
                    if (!storage.TryGetSingleForTypeAndEntity<WorldTemperatureAmplitudeChunk>(
                            chunkIdx,
                            out var chunk) || chunk == null) continue;

                    currentChunk = chunk;
                    lastCx = cx;
                    lastCy = cy;
                }

                if (currentChunk == null) continue;

                // 4. Extract data from the 32x32 slab
                var tempAmplitude = currentChunk.TemperatureAmplitude[(ly << 5) + lx];
                var index = Convert.ToInt32(9 * (tempAmplitude / 40f));
                // var index = Visual.GetScalarIndex(tempAmplitude, 0, 9);

                // 5. Map to your TUI visuals
                var marker = _markers[index];
                var (fg, bg) = _colors[index];

                // 6. Write to the VIEWPORT-relative buffer
                viewportBuffer[vY * viewportWidth + vX] = new CellVisual(marker, fg, bg);
            }
        }
    }
}

internal class BiomeVisualizer(Dictionary<Biome, CellVisual> biomeMap)
    : IWorldComponentVisualizer
{
    private readonly Dictionary<Biome, CellVisual> _biomeMap = biomeMap;

    public void SetVisuals(
        in IReadonlyStorage storage,
        in CellVisual[] viewportBuffer,
        int viewWorldX,
        int viewWorldY,
        int viewportWidth,
        int viewportHeight
    )
    {
        for (var vY = 0; vY < viewportHeight; vY++)
        {
            var worldY = viewWorldY + vY;

            // 1. Vertical Clamping (The Poles)
            // If the camera goes off the top/bottom, we draw nothing or "Void"
            if (worldY is < 0 or >= WorldMath.WorldHeight) continue;

            WorldBiomeChunk? currentChunk = null;
            var lastCx = -1;
            var lastCy = -1;

            for (var vX = 0; vX < viewportWidth; vX++)
            {
                // 2. Horizontal Wrapping (The Cylinder)
                // This handles negative viewWorldX (scrolling left) and > Width (scrolling right)
                var worldX = WorldMath.WrapX(viewWorldX + vX);

                // 3. Get Chunk and Local Coords
                var (cx, cy, lx, ly) = WorldMath.ToRelative(worldX, worldY);
                var chunkIdx = cy * WorldMath.ChunksAcross + cx;

                if (cx != lastCx || cy != lastCy)
                {
                    if (!storage.TryGetSingleForTypeAndEntity<WorldBiomeChunk>(chunkIdx,
                            out var chunk) || chunk == null) continue;

                    currentChunk = chunk;
                    lastCx = cx;
                    lastCy = cy;
                }

                if (currentChunk == null) continue;

                // 4. Extract data from the 32x32 slab
                var biome = currentChunk.Biome[(ly << 5) + lx];

                // 5. Map to your TUI visuals
                if (_biomeMap.TryGetValue(biome, out var marker))
                {
                    // 6. Write to the VIEWPORT-relative buffer
                    viewportBuffer[vY * viewportWidth + vX] = marker;
                }
            }
        }
    }
}

internal class RiverVisualizer(ConsoleColor riverFg, ConsoleColor defaultFg, ConsoleColor defaultBg)
    : IWorldComponentVisualizer
{
    private readonly ConsoleColor _riverFg = riverFg;
    private readonly ConsoleColor _defaultFg = defaultFg;
    private readonly ConsoleColor _defaultBg = defaultBg;

    public void SetVisuals(
        in IReadonlyStorage storage,
        in CellVisual[] viewportBuffer,
        int viewWorldX,
        int viewWorldY,
        int viewportWidth,
        int viewportHeight
    )
    {
        for (var vY = 0; vY < viewportHeight; vY++)
        {
            var worldY = viewWorldY + vY;

            // 1. Vertical Clamping (The Poles)
            // If the camera goes off the top/bottom, we draw nothing or "Void"
            if (worldY is < 0 or >= WorldMath.WorldHeight) continue;

            WorldRiverChunk? currentChunk = null;
            var lastCx = -1;
            var lastCy = -1;

            for (var vX = 0; vX < viewportWidth; vX++)
            {
                // 2. Horizontal Wrapping (The Cylinder)
                // This handles negative viewWorldX (scrolling left) and > Width (scrolling right)
                var worldX = WorldMath.WrapX(viewWorldX + vX);

                // 3. Get Chunk and Local Coords
                var (cx, cy, lx, ly) = WorldMath.ToRelative(worldX, worldY);
                var chunkIdx = cy * WorldMath.ChunksAcross + cx;

                if (cx != lastCx || cy != lastCy)
                {
                    if (!storage.TryGetSingleForTypeAndEntity<WorldRiverChunk>(chunkIdx,
                            out var chunk) || chunk == null) continue;

                    currentChunk = chunk;
                    lastCx = cx;
                    lastCy = cy;
                }

                if (currentChunk == null) continue;

                // 4. Extract data from the 32x32 slab
                var hasRiver = currentChunk.River[(ly << 5) + lx];

                // 5. Map to your TUI visuals
                var marker = hasRiver
                    ? new CellVisual(Cp437.Tilde, _riverFg, _defaultBg)
                    : new CellVisual(Cp437.WhiteSpace, _defaultFg, _defaultBg);

                // 6. Write to the VIEWPORT-relative buffer
                viewportBuffer[vY * viewportWidth + vX] = marker;
            }
        }
    }
}