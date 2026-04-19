using TermRTS.Examples.Greenery.Ecs.Component;
using TermRTS.Examples.Greenery.WorldGen;
using TermRTS.Io;
using TermRTS.Storage;

namespace TermRTS.Examples.Greenery.Ui;

internal static class Visual
{
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

            WorldPackedChunk? currentChunk = null;
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
                    if (!storage.TryGetSingleForTypeAndEntity<WorldPackedChunk>(chunkIdx,
                            out var chunk) || chunk == null) continue;

                    currentChunk = chunk;
                    lastCx = cx;
                    lastCy = cy;
                }

                if (currentChunk == null) continue;

                // 4. Extract data from the 32x32 slab
                var elevation = currentChunk.PackedTiles[(ly << 5) + lx].Elevation;

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

            WorldPackedChunk currentChunk = null;
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
                    if (!storage.TryGetSingleForTypeAndEntity<WorldPackedChunk>(chunkIdx,
                            out var chunk) || chunk == null) continue;

                    currentChunk = chunk;
                    lastCx = cx;
                    lastCy = cy;
                }

                if (currentChunk == null) continue;

                // 4. Extract data from the 32x32 slab
                var elevation = currentChunk.PackedTiles[(ly << 5) + lx].Elevation;
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

            WorldPackedChunk? currentChunk = null;
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
                    if (!storage.TryGetSingleForTypeAndEntity<WorldPackedChunk>(chunkIdx,
                            out var chunk) || chunk == null) continue;

                    currentChunk = chunk;
                    lastCx = cx;
                    lastCy = cy;
                }

                if (currentChunk == null) continue;

                // 4. Extract data from the 32x32 slab
                var feature = currentChunk.PackedTiles[(ly << 5) + lx].SurfaceFeature;

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

            WorldPackedChunk? currentChunk = null;
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
                    if (!storage.TryGetSingleForTypeAndEntity<WorldPackedChunk>(chunkIdx,
                            out var chunk) || chunk == null) continue;

                    currentChunk = chunk;
                    lastCx = cx;
                    lastCy = cy;
                }

                if (currentChunk == null) continue;

                // 4. Extract data from the 32x32 slab
                var temperature = currentChunk.PackedTiles[(ly << 5) + lx].Temperature;
                // TODO: Tie to world gen constants.
                var index = Visual.GetScalarIndex(temperature, -50, 35);

                // 5. Map to your TUI visuals
                var marker = markers[index];
                var (fg, bg) = colors[index];

                // 6. Write to the VIEWPORT-relative buffer
                viewportBuffer[vY * viewportWidth + vX] = new CellVisual(marker, fg, bg);
            }
        }
    }
}

internal class HumidityVisualizer(char[] markers, (ConsoleColor, ConsoleColor)[] colors)
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

            WorldPackedChunk? currentChunk = null;
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
                    if (!storage.TryGetSingleForTypeAndEntity<WorldPackedChunk>(chunkIdx,
                            out var chunk) || chunk == null) continue;

                    currentChunk = chunk;
                    lastCx = cx;
                    lastCy = cy;
                }

                if (currentChunk == null) continue;

                // 4. Extract data from the 32x32 slab
                var humidity = currentChunk.PackedTiles[(ly << 5) + lx].Humidity;
                var index = Convert.ToInt32(9 * (humidity / 100f));
                // var index = Visual.GetScalarIndex(humidity, 0, 9);

                // 5. Map to your TUI visuals
                var marker = markers[index];
                var (fg, bg) = colors[index];

                // 6. Write to the VIEWPORT-relative buffer
                viewportBuffer[vY * viewportWidth + vX] = new CellVisual(marker, fg, bg);
            }
        }
    }
}

internal class BiomeVisualizer(Dictionary<Biome, CellVisual> biomeMap)
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

            WorldPackedChunk? currentChunk = null;
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
                    if (!storage.TryGetSingleForTypeAndEntity<WorldPackedChunk>(chunkIdx,
                            out var chunk) || chunk == null) continue;

                    currentChunk = chunk;
                    lastCx = cx;
                    lastCy = cy;
                }

                if (currentChunk == null) continue;

                // 4. Extract data from the 32x32 slab
                var biome = currentChunk.PackedTiles[(ly << 5) + lx].Biome;

                // 5. Map to your TUI visuals
                if (biomeMap.TryGetValue(biome, out var marker))
                {
                    // 6. Write to the VIEWPORT-relative buffer
                    viewportBuffer[vY * viewportWidth + vX] = marker;
                }
            }
        }
    }
}

internal class WindVisualizer(WindDirectionTheme directionTheme)
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

            WorldPackedChunk? currentChunk = null;
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
                    if (!storage.TryGetSingleForTypeAndEntity<WorldPackedChunk>(chunkIdx,
                            out var chunk) || chunk == null) continue;

                    currentChunk = chunk;
                    lastCx = cx;
                    lastCy = cy;
                }

                if (currentChunk == null) continue;

                // 4. Extract data from the 32x32 slab
                var key = currentChunk.PackedTiles[(ly << 5) + lx].Wind;
                // 5. Map to your TUI visuals
                if (!directionTheme.WindDirectionMap.TryGetValue(key, out var value))
                {
                    value = new CellVisual(
                        Cp437.WhiteSpace,
                        ConsoleColor.Black,
                        ConsoleColor.Black);
                }

                // 6. Write to the VIEWPORT-relative buffer
                viewportBuffer[vY * viewportWidth + vX] = value;
            }
        }
    }
}
internal class WaterFlowVisualizer(DirectionMarkerTheme markerTheme, (ConsoleColor, ConsoleColor)[] colors)
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

            WorldPackedChunk? currentChunk = null;
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
                    if (!storage.TryGetSingleForTypeAndEntity<WorldPackedChunk>(chunkIdx,
                            out var chunk) || chunk == null) continue;

                    currentChunk = chunk;
                    lastCx = cx;
                    lastCy = cy;
                }

                if (currentChunk == null) continue;

                // 4. Extract data from the 32x32 slab
                var key = currentChunk.PackedTiles[(ly << 5) + lx].WaterFlow;
                var elevation = currentChunk.PackedTiles[(ly << 5) + lx].Elevation;
                // 5. Map to your TUI visuals
                if (!markerTheme.DirectionMarkerMap.TryGetValue(key, out var marker))
                {
                    marker = Cp437.WhiteSpace;
                }

                var cols = colors[elevation];

                // 6. Write to the VIEWPORT-relative buffer
                viewportBuffer[vY * viewportWidth + vX] = new CellVisual(marker, cols.Item1, cols.Item2);
            }
        }
    }
}