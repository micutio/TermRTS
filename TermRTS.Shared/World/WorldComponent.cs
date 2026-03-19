using TermRTS;

namespace TermRTS.Shared.World;

/// <summary>
///     ECS component holding a rectangular elevation/cell grid for procedural maps.
/// </summary>
public class WorldComponent(int entityId, int worldWidth, int worldHeight, byte[,] cells)
    : ComponentBase(entityId)
{
    public byte[,] Cells { get; } = cells;
    public int WorldWidth { get; } = worldWidth;
    public int WorldHeight { get; } = worldHeight;
}
