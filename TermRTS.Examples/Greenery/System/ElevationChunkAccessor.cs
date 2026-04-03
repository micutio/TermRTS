using TermRTS.Examples.Greenery.WorldGen;
using TermRTS.Storage;

namespace TermRTS.Examples.Greenery.System;

public ref struct ElevationChunkAccessor(in IReadonlyStorage storage)
{
    private readonly IReadonlyStorage _storage = storage;

    private WorldElevationChunk? _currentChunk = null;

    /// <summary>
    /// This assumes that bounds check has already been performed!
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public int GetValueAt(int x, int y)
    {
        var chunkId = WorldMath.GetChunkIndexFast(x, y);
        if (_currentChunk == null || _currentChunk.EntityId != chunkId)
        {
            if (!_storage.TryGetSingleForTypeAndEntity<WorldElevationChunk>(chunkId, out var chunk)
                || chunk == null)
                return int.MinValue;
            _currentChunk = chunk;
        }

        var (_, _, lx, ly) = WorldMath.ToRelative(x, y);
        return _currentChunk!.Elevation.Span[ly * WorldMath.ChunkSize + lx];
    }
}