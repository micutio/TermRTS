using System.Numerics;

namespace TermRTS.Examples.Greenery;

public class WorldComponent(int entityId, int worldWidth, int worldHeight, byte[,] cells)
    : ComponentBase(entityId)
{
    public byte[,] Cells { get; } = cells;
    public int Width { get; } = worldWidth;
    public int Height { get; } = worldHeight;
}

public class DroneComponent(int entityId, Vector2 position) : ComponentBase(entityId)
{
    public Vector2 Position { get; set; } = position;
}