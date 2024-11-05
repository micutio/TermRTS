namespace TermRTS.Examples.Greenery;

public class WorldComponent(int entityId, int worldWidth, int worldHeight, byte[,] cells)
    : ComponentBase(entityId)
{
    #region Constructor

    // TODO: Find out efficient matrix dimensions and iteration!

    #endregion

    public byte[,] Cells { get; } = cells;
    public int Width { get; } = worldWidth;
    public int Height { get; } = worldHeight;
}