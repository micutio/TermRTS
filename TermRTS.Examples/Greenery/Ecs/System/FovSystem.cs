using System.Numerics;
using TermRTS.Algorithms;
using TermRTS.Ecs;
using TermRTS.Event;
using TermRTS.Examples.Greenery.Ecs.Component;
using TermRTS.Examples.Greenery.WorldGen;
using TermRTS.Storage;

namespace TermRTS.Examples.Greenery.Ecs.System;

public class FovSystem : ISimSystem
{
    private readonly ChunkFov _fov = new();

    #region ISimSystem Members

    // TODO: Hand over viewport position IF we only want FOV in visible area.
    // TODO: Alternatively get chunk idx from drone positions.

    public void ProcessComponents(
        ulong timeStepSizeMs,
        in IReadonlyStorage storage,
        in List<ScheduledEvent> emittedEvents)
    {
        // TODO: Skip drones that haven't moved!
        // TODO: Change FOV component to chunks too!
        //if (!storage.TryGetSingleForType<FovComponent>(out var fov) || fov == null) return;

        var accessor = new ElevationChunkAccessor(in storage);

        // TODO: This is inefficient, but I don't have any better ideas right now.
        foreach (var fovChunk in storage.GetAllForType<FovChunk>())
        {
            fovChunk.Clear();
        }

        foreach (var dronePos in storage.GetAllForType<DroneComponent>()
                     .Select(drone => drone.Position))
        {
            PerformRaycastForDrone(dronePos, accessor);
            SetFovOnChunks(storage);
        }
    }

    #endregion

    #region Public Members

    public FovChunk[] InitializeFovChunks()
    {
        const int chunkSize = WorldMath.ChunkSize;
        const int worldWidth = WorldMath.WorldWidth;
        const int worldHeight = WorldMath.WorldHeight;

        // 1. Allocate ONE giant buffer for all chunk data combined
        // TODO: Make it more evident that masterBuffer will be in use for the duration
        //       of the game.
        var chunks = new FovChunk[WorldMath.ChunksAcross * (worldHeight / chunkSize)];


        for (var cy = 0; cy < worldHeight; cy += chunkSize)
            for (var cx = 0; cx < worldWidth; cx += chunkSize)
            {
                var chunkXIndex = cx / chunkSize;
                var chunkYIndex = cy / chunkSize;
                var chunkIdx = chunkYIndex * WorldMath.ChunksAcross + chunkXIndex;

                chunks[chunkIdx] =
                    new FovChunk(chunkIdx, chunkXIndex, chunkYIndex);
            }

        return chunks;
    }

    #endregion

    #region Private Members

    private void PerformRaycastForDrone(Vector2 dronePos, ElevationChunkAccessor chunkAccessor)
    {
        var droneX = (int)dronePos.X;
        var droneY = (int)dronePos.Y;
        _fov.BasicRaycast(
            droneX,
            droneY,
            10,
            chunkAccessor,
            (x, y, acc) =>
            {
                var wrappedX = WorldMath.WrapX(x);
                if (y is <= 0 or >= WorldMath.WorldHeight) return true;

                return acc.GetValueAt(wrappedX, y) > acc.GetValueAt(droneX, droneY);
            });
    }

    private void SetFovOnChunks(IReadonlyStorage storage)
    {
        var clearedChunks = new HashSet<int>();
        FovChunk? currentChunk = null;
        var lastCx = -1;
        var lastCy = -1;
        foreach (var (x, y) in _fov.VisibleCells)
        {
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
                    currentChunk.Clear();
                    clearedChunks.Add(chunkIdx);
                }
            }

            currentChunk?.SetVisible(lx, ly);
        }
    }

    #endregion
}