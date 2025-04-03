using System.Numerics;
using System.Text.Json.Serialization;
using TermRTS.Io;

namespace TermRTS.Examples.Greenery;

public class WorldComponent(int entityId, int worldWidth, int worldHeight, byte[,] cells)
    : ComponentBase(entityId)
{
    public byte[,] Cells { get; } = cells;
    public int WorldWidth { get; } = worldWidth;
    public int WorldHeight { get; } = worldHeight;
}

public class FovComponent: ComponentBase
{
    public FovComponent(int entityId, int worldWidth, int worldHeight): base(entityId)
    {
        Cells = new bool[worldWidth, worldHeight];
        WorldWidth = worldWidth;
        WorldHeight = worldHeight;
    }
    
    [JsonConstructor]
    internal FovComponent(int entityId, int worldWidth, int worldHeight, bool[,] cells): base(entityId)
    {
        Cells = cells;
        WorldWidth = worldWidth;
        WorldHeight = worldHeight;
    }
    
    public bool[,] Cells { get; }
    public int WorldWidth { get; }
    public int WorldHeight { get; }
}

public class DroneComponent : ComponentBase
{
    public const float Velocity = 1.0f; // [m/s]

    #region Private Fields

    private readonly DoubleBuffered<Vector2> _position;

    #endregion

    #region Constructor

    public DroneComponent(int entityId, Vector2 position) : base(entityId)
    {
        _position = new DoubleBuffered<Vector2>(position);
        RegisterDoubleBufferedProperty(_position);
    }

    #endregion

    public Vector2 Position
    {
        get => _position.Get();
        set => _position.Set(value);
    }

    #region Properties

    public List<Vector2>? Path { get; set; } // TODO: Change into Queue!

    public int? PathIndex { get; set; }

    public List<(int, int, char)> CachedPathVisual { get; } = [];

    #endregion

    public void ResetPath()
    {
        Path = null;
        PathIndex = null;
        CachedPathVisual.Clear();
    }

    /// <summary>
    ///     Generate a visual representation of the drone path.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown if Path is null.</exception>
    public void GeneratePathVisual()
    {
        if (Path == null) throw new ArgumentNullException(nameof(Path));

        var visual = new (int, int, char)[Path.Count];
        var positionCount = Path.Count;

        // Generate starting terminator
        var startChar = GenerateTerminatorChar(
            Path[0].X,
            Path[0].Y,
            Path[1].X,
            Path[1].Y
        );
        visual[0] = (Convert.ToInt32(Path[0].X), Convert.ToInt32(Path[0].Y), startChar);

        // Generate all parts in-between
        for (var i = 1; i < positionCount - 1; ++i)
        {
            var c = GeneratePathChar(
                Path[i].X,
                Path[i].Y,
                Path[i - 1].X,
                Path[i - 1].Y,
                Path[i + 1].X,
                Path[i + 1].Y
            );
            visual[i] = (Convert.ToInt32(Path[i].X), Convert.ToInt32(Path[i].Y), c);
        }

        // Generate ending terminator
        var endChar = GenerateTerminatorChar(
            Path[positionCount - 1].X,
            Path[positionCount - 1].Y,
            Path[positionCount - 2].X,
            Path[positionCount - 2].Y
        );
        visual[positionCount - 1] = (
            Convert.ToInt32(Path[positionCount - 1].X),
            Convert.ToInt32(Path[positionCount - 1].Y),
            endChar);

        CachedPathVisual.Clear();
        CachedPathVisual.AddRange(visual);
    }

    private static char GenerateTerminatorChar(float thisX, float thisY, float nextX, float nextY)
    {
        if (Math.Abs(thisX - nextX) > 0.0001)
            return thisX > nextX
                ? Cp437.BoxDoubleVerticalLeft
                : Cp437.BoxDoubleVerticalRight;
        return thisY > nextY
            ? Cp437.BoxUpDoubleHorizontal
            : Cp437.BoxDownDoubleHorizontal;
    }

    private static char GeneratePathChar(
        float thisX, float thisY, float prevX, float prevY, float nextX, float nextY)
    {
        Direction incoming;
        if (Math.Abs(thisX - prevX) > 0.0001)
            incoming = thisX > prevX
                ? Direction.West
                : Direction.East;
        else
            incoming = thisY > prevY
                ? Direction.North
                : Direction.South;

        Direction outgoing;
        if (Math.Abs(thisX - nextX) > 0.0001)
            outgoing = thisX > nextX
                ? Direction.West
                : Direction.East;
        else
            outgoing = thisY > nextY
                ? Direction.North
                : Direction.South;

        return (incoming, outgoing) switch
        {
            (Direction.North, Direction.East) or
                (Direction.East, Direction.North) => Cp437.BoxUpRight,
            (Direction.North, Direction.South) or
                (Direction.South, Direction.North) => Cp437.BoxVertical,
            (Direction.North, Direction.West) or
                (Direction.West, Direction.North) => Cp437.BoxUpLeft,
            (Direction.West, Direction.East) or
                (Direction.East, Direction.West) => Cp437.BoxHorizontal,
            (Direction.West, Direction.South) or
                (Direction.South, Direction.West) => Cp437.BoxDownLeft,
            (Direction.South, Direction.East) or
                (Direction.East, Direction.South) => Cp437.BoxDownRight,
            _ => '?'
        };
    }

    private enum Direction
    {
        North,
        East,
        South,
        West
    }
}