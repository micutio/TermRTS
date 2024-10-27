namespace TermRTS.Examples.Greenery;

public class WorldComponent : ComponentBase
{
    #region Fields

    private readonly Random _rng;

    #endregion

    #region Constructor

    // TODO: Find out efficient matrix dimensions and iteration!
    public WorldComponent(int entityId, int width, int height) : base(entityId)
    {
        Width = width;
        Height = height;
        Cells = new byte[width, height];
        _rng = new Random();

        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
            Cells[x, y] = (byte)_rng.Next(0, 255);
        // Cells[x, y] = Convert.ToByte((x + y) % 255);
    }

    #endregion

    public byte[,] Cells { get; }
    public int Width { get; }
    public int Height { get; }
}