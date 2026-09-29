using System.Text.Json.Serialization;
using TermRTS.Ecs;

namespace TermRTS.Examples.Greenery.Ecs.Component;

/// <summary>
///     FovChunk contains field of view and fog of war information for an entire chunk.
/// </summary>
/// <param name="entityId">Id of the ECS entity.</param>
/// <param name="cx">X-coordinate in the chunk grid.</param>
/// <param name="cy">Y-coordinate in the chunk grid.</param>
[method: JsonConstructor]
public class FovChunk(int entityId, int cx, int cy)
    : ComponentBase(entityId)
{
    // TODO: Fix all chunk sizes everywhere to 32.
    private const int ChunkSize = 32;

    private readonly uint[] _explored = new uint[ChunkSize];
    private readonly uint[] _visible = new uint[ChunkSize];

    // Chunk coordinate
    public int Cx { get; } = cx;
    public int Cy { get; } = cy;

    /// <summary>
    ///     Returns whether the cell under the chunk-local coordinate is explored.
    ///     This is always true if it is visible.
    /// </summary>
    /// <param name="localX">X-coordinate in the chunk-grid.</param>
    /// <param name="localY">Y-coordinate in the chunk-grid.</param>
    /// <returns>
    ///     <see Langword="true" /> if it is explored, <see Langword="false" /> otherwise.
    /// </returns>
    public bool IsExplored(int localX, int localY)
    {
        return (_explored[localY] & 1U << localX) != 0;
    }

    public void SetExplored(int localX, int localY)
    {
        _explored[localY] |= 1U << localX;
    }

    public bool IsVisible(int localX, int localY)
    {
        return (_visible[localY] & 1U << localX) != 0;
    }

    public void SetVisible(int localX, int localY)
    {
        _visible[localY] |= 1U << localX;
        _explored[localY] |= 1U << localX;
    }

    public void Clear()
    {
        for (var i = 0; i < ChunkSize; i++)
        {
            _visible[i] = 0;
        }
    }
}