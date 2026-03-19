using System.Numerics;
using ConsoleRenderer;
using TermRTS.Event;
using TermRTS.Io;
using TermRTS.Storage;
using TermRTS.Shared.Ui;
using TermRTS.Shared.World;
using TermRTS.Ui;

namespace TermRTS.Examples.Greenery.Ui;

public enum MapRenderMode
{
    ElevationColor,
    ElevationMonochrome,
    HeatMapColor,
    HeatMapMonochrome,
    ContourColor,
    ContourMonochrome,
    TerrainColor,
    TerrainMonochrome,
    ReliefColor,
    ReliefMonochrome
}

public class MapView : ViewportMapViewBase, IEventSink
{
    #region Fields

    #region Color Constants

    private static readonly TerminalColor DefaultBg = new(ConsoleColor.Black);
    private static readonly TerminalColor DefaultFg = new(ConsoleColor.Gray);

    private static readonly char[] MarkersElevation =
        ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];

    private static readonly char[] MarkersTerrain =
    [
        Cp437.Tilde,
        Cp437.Tilde,
        Cp437.Approximation,
        Cp437.Approximation,
        Cp437.SparseShade,
        Cp437.BoxDoubleUpHorizontal,
        Cp437.BoxUpHorizontal,
        Cp437.Intersection,
        Cp437.Caret,
        Cp437.TriangleUp
    ];

    private static readonly char[] MarkersHeatmapColor =
    [
        Cp437.DenseShade,
        Cp437.DenseShade,
        Cp437.DenseShade,
        Cp437.DenseShade,
        Cp437.DenseShade,
        Cp437.DenseShade,
        Cp437.DenseShade,
        Cp437.DenseShade,
        Cp437.DenseShade,
        Cp437.DenseShade
    ];

    private static readonly char[] MarkersHeatmapMonochrome =
    [
        Cp437.BlockFull,
        Cp437.SparseShade,
        Cp437.MediumShade,
        Cp437.DenseShade,
        Cp437.SparseShade,
        Cp437.MediumShade,
        Cp437.DenseShade,
        Cp437.MediumShade,
        Cp437.DenseShade,
        Cp437.BlockFull
    ];

    // Elevation colors: ConsoleColor equivalents as RGB (standard 16-color palette)
    private static readonly (TerminalColor, TerminalColor)[] ColorsElevation =
    [
        (new TerminalColor(0, 0, 128), DefaultBg), // DarkBlue
        (new TerminalColor(0, 0, 255), DefaultBg), // Blue
        (new TerminalColor(0, 128, 128), DefaultBg), // DarkCyan
        (new TerminalColor(0, 255, 255), DefaultBg), // Cyan
        (new TerminalColor(255, 255, 0), DefaultBg), // Yellow
        (new TerminalColor(0, 128, 0), DefaultBg), // DarkGreen
        (new TerminalColor(0, 255, 0), DefaultBg), // Green
        (new TerminalColor(128, 128, 0), DefaultBg), // DarkYellow
        (new TerminalColor(128, 128, 128), DefaultBg), // DarkGray
        (new TerminalColor(192, 192, 192), DefaultBg) // Gray
    ];

    private static readonly (TerminalColor, TerminalColor)[] ColorsElevationMonochrome =
    [
        (DefaultFg, DefaultBg),
        (DefaultFg, DefaultBg),
        (DefaultFg, DefaultBg),
        (DefaultFg, DefaultBg),
        (DefaultFg, DefaultBg),
        (DefaultFg, DefaultBg),
        (DefaultFg, DefaultBg),
        (DefaultFg, DefaultBg),
        (DefaultFg, DefaultBg),
        (DefaultFg, DefaultBg)
    ];

    private static readonly (TerminalColor, TerminalColor)[] ColorsHeatmapMonochrome =
    [
        (new TerminalColor(ConsoleColor.Black), new TerminalColor(ConsoleColor.Black)),
        (new TerminalColor(ConsoleColor.DarkGray), new TerminalColor(ConsoleColor.Black)),
        (new TerminalColor(ConsoleColor.DarkGray), new TerminalColor(ConsoleColor.Black)),
        (new TerminalColor(ConsoleColor.DarkGray), new TerminalColor(ConsoleColor.Black)),
        (new TerminalColor(ConsoleColor.Gray), new TerminalColor(ConsoleColor.DarkGray)),
        (new TerminalColor(ConsoleColor.Gray), new TerminalColor(ConsoleColor.DarkGray)),
        (new TerminalColor(ConsoleColor.Gray), new TerminalColor(ConsoleColor.DarkGray)),
        (new TerminalColor(ConsoleColor.White), new TerminalColor(ConsoleColor.DarkGray)),
        (new TerminalColor(ConsoleColor.White), new TerminalColor(ConsoleColor.DarkGray)),
        (new TerminalColor(ConsoleColor.White), new TerminalColor(ConsoleColor.DarkGray))
    ];

    #endregion

    // cached world and drone paths
    private readonly (byte, char)[,] _cachedWorld;
    private readonly bool[,] _cachedFov;
    private readonly Dictionary<int, Vector2> _cachedDronePositions;
    private readonly Dictionary<int, List<(int, int, char)>> _cachedDronePaths;

    // rendering options
    private MapRenderMode _mapRenderMode = MapRenderMode.ElevationColor;

    #endregion

    #region Constructor

    public MapView(ConsoleCanvas canvas, int worldWidth, int worldHeight) : base(canvas, worldWidth,
        worldHeight)
    {
        _cachedWorld = new (byte, char)[worldWidth, worldHeight];
        _cachedFov = new bool[worldWidth, worldHeight];
        _cachedDronePaths = new Dictionary<int, List<(int, int, char)>>();
        _cachedDronePositions = new Dictionary<int, Vector2>();
    }

    #endregion

    #region Private Properties

    private MapRenderMode MapRenderMode
    {
        get => _mapRenderMode;
        set
        {
            _mapRenderMode = value;
            IsRequireReRender = true;
        }
    }

    #endregion

    #region IUiElement Members

    public override void UpdateFromComponents(
        in IReadonlyStorage componentStorage,
        double timeStepSizeMs,
        double howFarIntoNextFramePercent)
    {
        if (!componentStorage.TryGetSingleForType<WorldComponent>(out var world) ||
            world == null) return;
        if (!componentStorage.TryGetSingleForType<FovComponent>(out var fov) || fov == null) return;

        switch (MapRenderMode)
        {
            case MapRenderMode.ElevationColor:
            case MapRenderMode.ElevationMonochrome:
                SetElevationVisual(world);
                break;
            case MapRenderMode.HeatMapColor:
                SetHeatmapColorVisual(world);
                break;
            case MapRenderMode.HeatMapMonochrome:
                SetHeatmapMonochromeVisual(world);
                break;
            case MapRenderMode.ContourColor:
            case MapRenderMode.ContourMonochrome:
                SetContourLines(world);
                break;
            case MapRenderMode.TerrainColor:
            case MapRenderMode.TerrainMonochrome:
                SetTerrainVisual(world);
                break;
            case MapRenderMode.ReliefColor:
            case MapRenderMode.ReliefMonochrome:
                SetReliefVisual(world);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        for (var y = 0; y < WorldHeight; y++)
        for (var x = 0; x < WorldWidth; x++)
        {
            if (_cachedFov[x, y] == fov.Cells[x, y]) continue;
            IsRequireReRender = true;
            _cachedFov[x, y] = fov.Cells[x, y];
        }

        foreach (var drone in componentStorage.GetAllForType<DroneComponent>())
        {
            if (drone.Path != null)
            {
                if (_cachedDronePaths.TryGetValue(drone.EntityId, out var path))
                {
                    if (path.Count != drone.CachedPathVisual.Count) IsRequireReRender = true;

                    path.Clear();
                    path.AddRange(drone.CachedPathVisual);
                }
                else
                {
                    _cachedDronePaths.Add(drone.EntityId, [.. drone.CachedPathVisual]);
                    IsRequireReRender = true;
                }
            }

            _cachedDronePositions[drone.EntityId] = drone.Position;
        }
    }

    public override void Render()
    {
        var viewportExtendInWorldX = ViewportPositionInWorldX + ViewportWidth;
        var viewportExtendInWorldY = ViewportPositionInWorldY + ViewportHeight;
        var boundaryX = Math.Min(WorldWidth, viewportExtendInWorldX);
        var boundaryY = Math.Min(WorldHeight, viewportExtendInWorldY);

        // Step 1: Render World
        for (var y = ViewportPositionInWorldY; y < boundaryY; y++)
        for (var x = ViewportPositionInWorldX; x < boundaryX; x++)
        {
            var (elevation, c) = _cachedWorld[x, y];
            var isFov = _cachedFov[x, y];
            var colors = MapRenderMode switch
            {
                MapRenderMode.ElevationColor => ColorsElevation,
                MapRenderMode.HeatMapColor => ColorsElevation,
                MapRenderMode.ContourColor => ColorsElevation,
                MapRenderMode.TerrainColor => ColorsElevation,
                MapRenderMode.ReliefColor => ColorsElevation,
                MapRenderMode.ElevationMonochrome => ColorsElevationMonochrome,
                MapRenderMode.HeatMapMonochrome => ColorsHeatmapMonochrome,
                MapRenderMode.ContourMonochrome => ColorsElevationMonochrome,
                MapRenderMode.TerrainMonochrome => ColorsElevationMonochrome,
                MapRenderMode.ReliefMonochrome => ColorsElevationMonochrome,
                _ => throw new ArgumentOutOfRangeException()
            };
            var (colFg, colBg) = colors[elevation];
            Canvas.Set(
                X + WorldToViewportX(x) + SpaceForScaleLeftValue,
                Y + WorldToViewportY(y) + SpaceForScaleTop,
                c,
                isFov ? colFg : DefaultFg,
                isFov ? colBg : DefaultBg);
        }

        // Step 2: Render drones and drone paths
        foreach (var path in _cachedDronePaths.Values)
        foreach (var (pathX, pathY, pathCol) in path)
            if (IsInCamera(pathX, pathY))
                Canvas.Set(
                    X + WorldToViewportX(pathX) + SpaceForScaleLeftValue,
                    Y + WorldToViewportY(pathY) + SpaceForScaleTop,
                    pathCol,
                    new TerminalColor(ConsoleColor.Red),
                    DefaultBg);

        foreach (var pos in _cachedDronePositions.Values)
        {
            var droneX = Convert.ToInt32(pos.X);
            var droneY = Convert.ToInt32(pos.Y);
            if (IsInCamera(droneX, droneY))
                Canvas.Set(
                    X + WorldToViewportX(droneX) + SpaceForScaleLeftValue,
                    Y + WorldToViewportY(droneY) + SpaceForScaleTop,
                    '@',
                    DefaultBg,
                    new TerminalColor(ConsoleColor.Red));
        }

        // Step 3: Render Coordinate Scales at the top and left sides.
        RenderCoordinates();
    }

    #endregion

    #region IEventSink Members

    public void ProcessEvent(IEvent evt)
    {
        if (evt is Event<MapRenderMode>(var renderMode)) MapRenderMode = renderMode;
    }

    #endregion

    #region Private Members

    private void SetElevationVisual(WorldComponent world)
    {
        for (var y = 0; y < WorldHeight; y++)
        for (var x = 0; x < WorldWidth; x++)
            _cachedWorld[x, y] = (world.Cells[x, y], MarkersElevation[world.Cells[x, y]]);
    }

    private void SetHeatmapColorVisual(WorldComponent world)
    {
        for (var y = 0; y < WorldHeight; y++)
        for (var x = 0; x < WorldWidth; x++)
            _cachedWorld[x, y] = (world.Cells[x, y], MarkersHeatmapColor[world.Cells[x, y]]);
    }

    private void SetHeatmapMonochromeVisual(WorldComponent world)
    {
        for (var y = 0; y < WorldHeight; y++)
        for (var x = 0; x < WorldWidth; x++)
            _cachedWorld[x, y] = (world.Cells[x, y], MarkersHeatmapMonochrome[world.Cells[x, y]]);
    }

    private void SetTerrainVisual(WorldComponent world)
    {
        for (var y = 0; y < WorldHeight; y++)
        for (var x = 0; x < WorldWidth; x++)
            _cachedWorld[x, y] = (world.Cells[x, y], MarkersTerrain[world.Cells[x, y]]);
    }

    private void SetReliefVisual(WorldComponent world)
    {
        for (var y = 0; y < WorldHeight; y++)
        for (var x = 0; x < WorldWidth; x++)
        {
            char c;
            if (IsInBounds(x - 1, y) && world.Cells[x - 1, y] < world.Cells[x, y] &&
                world.Cells[x, y] > 3)
                c = Cp437.Slash;
            else if (IsInBounds(x + 1, y) && world.Cells[x + 1, y] < world.Cells[x, y] &&
                     world.Cells[x, y] > 3)
                c = Cp437.BackSlash;
            else if (world.Cells[x, y] <= 3)
                c = '~';
            else
                c = Cp437.Interpunct;

            _cachedWorld[x, y] = (world.Cells[x, y], c);
        }
    }

    private void SetContourLines(WorldComponent world)
    {
        for (var y = 0; y < WorldHeight; y++)
        for (var x = 0; x < WorldWidth; x++)
        {
            var cell = world.Cells[x, y];
            if (cell < 3)
            {
                _cachedWorld[x, y] = (cell, Cp437.WhiteSpace);
                continue;
            }

            var c = GetCharFromCliffs(cell, x, y, world);
            _cachedWorld[x, y] = (cell, c);
        }

        for (var y = 0; y < WorldHeight; y++)
        for (var x = 0; x < WorldWidth; x++)
        {
            var cell = world.Cells[x, y];
            if (cell < 3) continue;

            if (_cachedWorld[x, y].Item2 == 'X') continue;
            var c = GetCliffAdjacentChar(cell, x, y, world);

            _cachedWorld[x, y].Item2 = c;
        }

        for (var y = 0; y < WorldHeight; y++)
        for (var x = 0; x < WorldWidth; x++)
        {
            var cell = world.Cells[x, y];
            if (cell < 3) continue;

            if (_cachedWorld[x, y].Item2 != 'X') continue;
            var c = GetCliffChar(cell, x, y, world);

            _cachedWorld[x, y].Item2 = c;
        }
    }

    private char GetCharFromCliffs(byte cell, int x, int y, WorldComponent world)
    {
        // north
        byte? north = IsInBounds(x, y - 1) ? world.Cells[x, y - 1] : null;
        if (north < cell) return 'X';

        // east
        byte? east = IsInBounds(x + 1, y) ? world.Cells[x + 1, y] : null;
        if (east < cell) return 'X';

        // south
        byte? south = IsInBounds(x, y + 1) ? world.Cells[x, y + 1] : null;
        if (south < cell) return 'X';

        // west
        byte? west = IsInBounds(x - 1, y) ? world.Cells[x - 1, y] : null;
        if (west < cell) return 'X';

        return Cp437.WhiteSpace;
    }

    private char GetCliffAdjacentChar(byte cell, int x, int y, WorldComponent world)
    {
        //return _visualByPosition[x, y].Item1;
        var b = 0b_0000_0000;
        // north
        byte? north = IsInBounds(x, y - 1) ? world.Cells[x, y - 1] : null;
        if (north == cell && _cachedWorld[x, y - 1].Item2 == Cp437.UpperX) b |= 0b_0000_1000;

        // east
        byte? east = IsInBounds(x + 1, y) ? world.Cells[x + 1, y] : null;
        if (east == cell && _cachedWorld[x + 1, y].Item2 == Cp437.UpperX) b |= 0b_0000_0100;

        // south
        byte? south = IsInBounds(x, y + 1) ? world.Cells[x, y + 1] : null;
        if (south == cell && _cachedWorld[x, y + 1].Item2 == Cp437.UpperX) b |= 0b_0000_0010;

        // west
        byte? west = IsInBounds(x - 1, y) ? world.Cells[x - 1, y] : null;
        if (west == cell && _cachedWorld[x - 1, y].Item2 == Cp437.UpperX) b |= 0b_0000_0001;

        return b switch
        {
            0 => Cp437.WhiteSpace, // 0000
            1 => Cp437.WhiteSpace, // 0001, west
            2 => Cp437.WhiteSpace, // 0010, south
            3 => Cp437.BoxDownLeft, // 0011, southwest
            4 => Cp437.WhiteSpace, // 0100, east
            5 => Cp437.BoxHorizontal, // 0101, east west
            6 => Cp437.BoxDownRight, // 0110, southeast
            7 => Cp437.BoxDownHorizontal, // 0111, east-south-west
            8 => Cp437.WhiteSpace, // 1000, north
            9 => Cp437.BoxUpLeft, // 1001, northwest
            10 => Cp437.BoxVertical, // 1010, north-south
            11 => Cp437.BoxVerticalLeft, // 1011, north-south-west
            12 => Cp437.BoxUpRight, // 1100, northeast
            13 => Cp437.BoxUpHorizontal, // 1101, north-east-west
            14 => Cp437.BoxVerticalRight, // 1110, north-south-east
            15 => Cp437.BoxVerticalHorizontal, // 1111, all
            _ => '?'
        };
    }

    private char GetCliffChar(byte cell, int x, int y, WorldComponent world)
    {
        //return _visualByPosition[x, y].Item1;
        var b = 0b_0000_0000;
        // north
        byte? north = IsInBounds(x, y - 1) ? world.Cells[x, y - 1] : null;
        if (north == cell && _cachedWorld[x, y - 1].Item2 != Cp437.WhiteSpace)
            b |= 0b_0000_1000;

        // east
        byte? east = IsInBounds(x + 1, y) ? world.Cells[x + 1, y] : null;
        if (east == cell && _cachedWorld[x + 1, y].Item2 != Cp437.WhiteSpace)
            b |= 0b_0000_0100;

        // south
        byte? south = IsInBounds(x, y + 1) ? world.Cells[x, y + 1] : null;
        if (south == cell && _cachedWorld[x, y + 1].Item2 != Cp437.WhiteSpace)
            b |= 0b_0000_0010;

        // west
        byte? west = IsInBounds(x - 1, y) ? world.Cells[x - 1, y] : null;
        if (west == cell && _cachedWorld[x - 1, y].Item2 != Cp437.WhiteSpace)
            b |= 0b_0000_0001;

        if (b == 0 && _cachedWorld[x, y].Item2 == Cp437.WhiteSpace) return Cp437.WhiteSpace;

        return b switch
        {
            0 => Cp437.BulletHollow, // 0000
            1 => Cp437.BoxHorizontal, // 0001, west
            2 => Cp437.BoxVertical, // 0010, south
            3 => Cp437.BoxDownLeft, // 0011, southwest
            4 => Cp437.BoxHorizontal, // 0100, east
            5 => Cp437.BoxHorizontal, // 0101, east west
            6 => Cp437.BoxDownRight, // 0110, southeast
            7 => Cp437.BoxDownHorizontal, // 0111, east-south-west
            8 => Cp437.BoxVertical, // 1000, north
            9 => Cp437.BoxUpLeft, // 1001, northwest
            10 => Cp437.BoxVertical, // 1010, north-south
            11 => Cp437.BoxVerticalLeft, // 1011, north-south-west
            12 => Cp437.BoxUpRight, // 1100, northeast
            13 => Cp437.BoxUpHorizontal, // 1101, north-east-west
            14 => Cp437.BoxVerticalRight, // 1110, north-south-east
            15 => Cp437.BoxVerticalHorizontal, // 1111, all
            _ => '?'
        };
    }

    #endregion
}