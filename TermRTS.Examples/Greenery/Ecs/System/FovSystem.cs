using TermRTS.Algorithms;
using TermRTS.Examples.Greenery.Ecs.Component;
using TermRTS.Examples.Greenery.WorldGen;
using TermRTS.Storage;

namespace TermRTS.Examples.Greenery.System;

public class FovSystem : ISimSystem
{
    private readonly ChunkFov _fov = new();

    #region ISimSystem Members

    // TODO: Hand over viewport position IF we only want FOV in visible area.
    // TODO: Alternatively get chunk idx from drone positions.

    public void ProcessComponents(ulong timeStepSizeMs, in IReadonlyStorage storage)
    {
        // TODO: Skip drones that haven't moved!
        // TODO: Change FOV component to chunks too!
        //if (!storage.TryGetSingleForType<FovComponent>(out var fov) || fov == null) return;

        var accessor = new ElevationChunkAccessor(in storage);
        var clearedChunks = new HashSet<int>();

        foreach (var dronePos in storage.GetAllForType<DroneComponent>()
                     .Select(drone => drone.Position))
        {
            var droneX = (int)dronePos.X;
            var droneY = (int)dronePos.Y;
            _fov.BasicRaycast(
                droneX,
                droneY,
                10,
                accessor,
                (x, y, acc) =>
                {
                    var wrappedX = WorldMath.WrapX(x);
                    if (y is <= 0 or >= WorldMath.WorldHeight) return true;

                    return acc.GetValueAt(wrappedX, y) > acc.GetValueAt(droneX, droneY);
                });

            FovChunk? currentChunk = null;
            var lastCx = -1;
            var lastCy = -1;
            foreach (var (x, y) in _fov.VisibleCells)
            {
                // fov.Cells[WorldMath.WrapX(x), y] = true;
                var worldX = WorldMath.WrapX(x);

                // Get Chunk and Local Coords
                var (cx, cy, lx, ly) = WorldMath.ToRelative(worldX, y);
                var chunkIdx = cy * WorldMath.ChunksAcross + cx;

                if (cx != lastCx || cy != lastCy)
                {
                    if (!storage.TryGetSingleForTypeAndEntity<FovChunk>(chunkIdx,
                            out var chunk) || chunk == null) continue;

                    currentChunk = chunk;
                    lastCx = cx;
                    lastCy = cy;

                    // clear chunk if we access it the first time
                    if (!clearedChunks.Contains(chunkIdx))
                    {
                        currentChunk.FovField.Span.Clear();
                        clearedChunks.Add(chunkIdx);
                    }
                }

                currentChunk?.FovField.Span[ly * WorldMath.ChunkSize + cx] = true;
            }
        }
    }

    #endregion

    #region Public Members

    public FovChunk[] InitializeFovChunks()
    {
        const int chunkSize = WorldMath.ChunkSize;
        const int worldWidth = WorldMath.WorldWidth;
        const int worldHeight = WorldMath.WorldHeight;
        const int totalCells = worldWidth * worldHeight;

        // 1. Allocate ONE giant buffer for all chunk data combined
        // TODO: Make it more evident that masterBuffer will be in use for the duration
        //       of the game.
        var masterBuffer = new bool[totalCells];
        var masterSpan = masterBuffer.AsMemory();
        var chunks = new FovChunk[WorldMath.ChunksAcross * (worldHeight / chunkSize)];


        for (var cy = 0; cy < worldHeight; cy += chunkSize)
            for (var cx = 0; cx < worldWidth; cx += chunkSize)
            {
                var chunkXIndex = cx / chunkSize;
                var chunkYIndex = cy / chunkSize;
                var chunkIdx = chunkYIndex * WorldMath.ChunksAcross + chunkXIndex;

                // Calculate where this chunk starts in our new Master Buffer
                var masterStart = chunkIdx * chunkSize * chunkSize;
                var chunkMemorySegment = masterSpan.Slice(masterStart, chunkSize * chunkSize);

                // The chunk now holds a 'view' of the master buffer, not a unique array
                chunks[chunkIdx] =
                    new FovChunk(chunkIdx, chunkXIndex, chunkYIndex, chunkMemorySegment);
            }

        return chunks;
    }

    #endregion
}