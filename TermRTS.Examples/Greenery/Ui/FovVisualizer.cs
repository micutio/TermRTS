using TermRTS.Examples.Greenery.Ecs.Component;
using TermRTS.Examples.Greenery.WorldGen;
using TermRTS.Storage;

namespace TermRTS.Examples.Greenery.Ui;

internal class FovVisualizer()
{
    public void CacheFov(
        in IReadonlyStorage storage,
        in bool[] cachedFov,
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

            FovChunk? currentChunk = null;
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
                    if (!storage.TryGetSingleForTypeAndEntity<FovChunk>(chunkIdx,
                            out var chunk) || chunk == null) continue;

                    currentChunk = chunk;
                    lastCx = cx;
                    lastCy = cy;
                }

                if (currentChunk == null) continue;

                // 4. Extract data from the 32x32 slab
                var isFov = currentChunk.FovField.Span[(ly << 5) + lx];

                // 6. Write to the VIEWPORT-relative buffer
                cachedFov[vY * viewportWidth + vX] = isFov;
            }
        }
    }
}