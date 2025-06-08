using System.Numerics;
using ConsoleRenderer;
using TermRTS.Event;
using TermRTS.Io;
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

public class MapView : KeyInputProcessorBase, IEventSink
{
    #region Fields

    private static readonly ConsoleColor DefaultBg = Console.BackgroundColor;
    private static readonly ConsoleColor DefaultFg = Console.ForegroundColor;
    private readonly ConsoleCanvas _canvas;
    private readonly (char, ConsoleColor, ConsoleColor)[] _visualByElevation;
    private readonly (char, ConsoleColor, ConsoleColor)[,] _visualByPosition;

    #region Positioning Constants and Variables

    // World size
    private readonly int _worldWidth;

    private readonly int _worldHeight;

    // Offsets for the Map rendering, to accommodate left and top scales
    private readonly int _spaceForScaleTop = 1;
    private int _spaceForScaleLeft;

    // Left top position of the camera within the world
    private int _viewportPositionInWorldX;
    private int _viewportPositionInWorldY;

    #endregion

    // rendering options
    private MapRenderMode _mapRenderMode = MapRenderMode.ElevationColor;
    private bool _initVisualMatrix = true;

    #endregion

    #region Constructor

    public MapView(ConsoleCanvas canvas, int worldWidth, int worldHeight)
    {
        _canvas = canvas;
        _canvas.AutoResize = true;
        // _canvas.Interlaced = true;

        _visualByElevation = new (char, ConsoleColor, ConsoleColor)[10];
        _visualByPosition = new (char, ConsoleColor, ConsoleColor)[worldWidth, worldHeight];
        _worldWidth = worldWidth;
        _worldHeight = worldHeight;

        _viewportPositionInWorldX = 0;
        _viewportPositionInWorldY = 0;

        SetElevationLevelColorVisual();
        Console.CursorVisible = false;
    }

    #endregion

    #region Private Properties

    private MapRenderMode MapRenderMode
    {
        get => _mapRenderMode;
        set
        {
            _mapRenderMode = value;
            switch (_mapRenderMode)
            {
                case MapRenderMode.ElevationColor:
                    SetElevationLevelColorVisual();
                    break;
                case MapRenderMode.ElevationMonochrome:
                    SetElevationLevelMonochromeVisual();
                    break;
                case MapRenderMode.HeatMapColor:
                    SetHeatmapColorVisual();
                    break;
                case MapRenderMode.HeatMapMonochrome:
                    SetHeatmapMonochromeVisual();
                    break;
                case MapRenderMode.TerrainColor:
                    SetTerrainColorVisual();
                    break;
                case MapRenderMode.ReliefColor:
                case MapRenderMode.ContourColor:
                    SetTerrainColorVisual();
                    _initVisualMatrix = true;
                    break;
                case MapRenderMode.TerrainMonochrome:
                    SetTerrainMonochromeVisual();
                    break;
                case MapRenderMode.ReliefMonochrome:
                case MapRenderMode.ContourMonochrome:
                    SetTerrainMonochromeVisual();
                    _initVisualMatrix = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private int ViewPortWidth => Width - _spaceForScaleLeft;
    private int ViewPortHeight => Height - _spaceForScaleTop;

    #endregion

    #region IUiElement Members

    public override void UpdateFromComponents(
        in IStorage componentStorage,
        double timeStepSizeMs,
        double howFarIntoNextFramePercent)
    {
        // TODO:
        throw new NotImplementedException();
    }

    public override void Render()
    {
        // TODO:
        throw new NotImplementedException();
    }

    protected override void OnXChanged(int newX)
    {
        IsRequireReRender = true;
        IsRequireRootReRender = true;
    }

    protected override void OnYChanged(int newY)
    {
        IsRequireReRender = true;
        IsRequireRootReRender = true;
    }

    protected override void OnWidthChanged(int newWidth)
    {
        IsRequireReRender = true;
        IsRequireRootReRender = true;
    }

    protected override void OnHeightChanged(int newHeight)
    {
        UpdateSpaceForScaleLeft();
        IsRequireReRender = true;
        IsRequireRootReRender = true;
    }

    #endregion

    #region IEventSink Members

    public void ProcessEvent(IEvent evt)
    {
        if (evt is Event<MapRenderMode> (var renderMode)) MapRenderMode = renderMode;
    }

    #endregion

    // TODO: Remove IRenderer Members, not implementing that anymore.

    #region IRenderer Members

    public void RenderComponents(
        in IStorage storage,
        double timeStepSizeMs,
        double howFarIntoNextFramePercent)
    {
        // Step 1: Render world
        var world = storage.GetSingleForType<WorldComponent>();
        if (world == null) return;
        var fov = storage.GetSingleForType<FovComponent>();
        if (fov == null) return;

        switch (MapRenderMode)
        {
            case MapRenderMode.ElevationColor:
            case MapRenderMode.ElevationMonochrome:
            case MapRenderMode.HeatMapColor:
            case MapRenderMode.HeatMapMonochrome:
            case MapRenderMode.TerrainColor:
            case MapRenderMode.TerrainMonochrome:
                RenderWorldByElevationVisuals(world, fov);
                break;
            case MapRenderMode.ReliefColor:
            case MapRenderMode.ReliefMonochrome:
                if (_initVisualMatrix)
                {
                    SetWorldReliefVisual(world);
                    _initVisualMatrix = false;
                }

                RenderWorldByVisualMatrix(fov);
                break;
            case MapRenderMode.ContourColor:
            case MapRenderMode.ContourMonochrome:
                if (_initVisualMatrix)
                {
                    SetWorldContourLines(world);
                    _initVisualMatrix = false;
                }

                RenderWorldByVisualMatrix(fov);
                break;
            default:
                throw new ArgumentOutOfRangeException(MapRenderMode.ToString());
        }

        // Step 2: Render drone
        foreach (var drone in storage.GetAllForType<DroneComponent>())
        {
            if (drone.Path != null)
                foreach (var (pathX, pathY, pathCol) in drone.CachedPathVisual)
                    if (IsInCamera(pathX, pathY))
                        _canvas.Set(
                            X + WorldToViewportX(pathX) + _spaceForScaleLeft,
                            Y + WorldToViewportY(pathY) + _spaceForScaleTop,
                            pathCol,
                            ConsoleColor.Red,
                            DefaultBg);

            var droneX = Convert.ToInt32(drone.Position.X);
            var droneY = Convert.ToInt32(drone.Position.Y);
            if (IsInCamera(droneX, droneY))
                _canvas.Set(
                    X + WorldToViewportX(droneX) + _spaceForScaleLeft,
                    X + WorldToViewportY(droneY) + _spaceForScaleTop,
                    '@',
                    DefaultBg,
                    ConsoleColor.Red);
        }

        RenderCoordinates();
    }

    #endregion

    #region KeyInputProcessorBase Members

    public override void HandleKeyInput(in ConsoleKeyInfo keyInfo)
    {
        switch (keyInfo.Key)
        {
            case ConsoleKey.UpArrow:
                MoveCameraUp();
                return;
            case ConsoleKey.DownArrow:
                MoveCameraDown();
                return;
            case ConsoleKey.LeftArrow:
                MoveCameraLeft();
                return;
            case ConsoleKey.RightArrow:
                MoveCameraRight();
                return;
            default:
                return;
        }
    }

    #endregion

    #region Private Members

    private void UpdateSpaceForScaleLeft()
    {
        _spaceForScaleLeft = (_viewportPositionInWorldY + ViewPortHeight) switch
        {
            < 10 => 1,
            < 100 => 2,
            < 1000 => 3,
            < 10000 => 4,
            _ => 5
        };
    }

    // TODO: Turn _viewportPositionInWorldY into a property and put this check into the setter!
    private void MoveCameraUp()
    {
        var newPos = Math.Max(_viewportPositionInWorldY - 1, 0);
        if (newPos == _viewportPositionInWorldY) return;

        _viewportPositionInWorldY = newPos;
        UpdateSpaceForScaleLeft();
        IsRequireReRender = true;
    }

    // TODO: Turn _viewportPositionInWorldY into a property and put this check into the setter!
    private void MoveCameraDown()
    {
        var newPos = Math.Min(_viewportPositionInWorldY + 1, _worldHeight - 1);
        if (newPos == _viewportPositionInWorldY) return;

        _viewportPositionInWorldY = newPos;
        UpdateSpaceForScaleLeft();
        IsRequireReRender = true;
    }

    // TODO: Turn _viewportPositionInWorldX into a property and put this check into the setter!
    private void MoveCameraLeft()
    {
        var newPos = Math.Max(_viewportPositionInWorldX - 1, 0);
        if (newPos == _viewportPositionInWorldX) return;

        _viewportPositionInWorldX = newPos;
        IsRequireReRender = true;
    }

    // TODO: Turn _viewportPositionInWorldX into a property and put this check into the setter!
    private void MoveCameraRight()
    {
        // TODO: Fix bug in case where viewport is larger than world!
        var newPos = Math.Min(_viewportPositionInWorldX + 1, _worldHeight - 1);
        if (newPos == _viewportPositionInWorldX) return;

        _viewportPositionInWorldX = newPos;
        IsRequireReRender = true;
    }

    /// <summary>
    /// Determine whether a position is within the viewport boundaries.
    /// </summary>
    /// <param name="x">x-position relative to world coordinates</param>
    /// <param name="y">y-position relative to world coordinates</param>
    /// <returns><c>true</c> if it is within the viewport, <c>false</c> otherwise.</returns>
    private bool IsInCamera(float x, float y)
    {
        return x >= _viewportPositionInWorldX
               && x <= _viewportPositionInWorldX + ViewPortWidth
               && y >= _viewportPositionInWorldY
               && y <= _viewportPositionInWorldY + ViewPortHeight;
    }

    /// <summary>
    /// Determine whether a position is within the world or not
    /// </summary>
    /// <param name="x">x-position relative to world coordinates</param>
    /// <param name="y">y-position relative to world coordinates</param>
    /// <returns></returns>
    private bool IsInBounds(float x, float y)
    {
        return x >= 0
               && x < _worldWidth
               && y >= 0
               && y < _worldHeight;
    }

    private int WorldToViewportX(float x)
    {
        return Convert.ToInt32(x - _viewportPositionInWorldX);
    }

    private int WorldToViewportY(float y)
    {
        return Convert.ToInt32(y - _viewportPositionInWorldY);
    }

    private int ViewportToWorldX(int x)
    {
        return _viewportPositionInWorldX + x;
    }

    private float ViewportToWorldY(float y)
    {
        return _viewportPositionInWorldY + y;
    }

    private void RenderWorldByElevationVisuals(in WorldComponent world, in FovComponent fov)
    {
        for (var y = _viewportPositionInWorldY; y < ViewPortHeight; y++)
        for (var x = _viewportPositionInWorldX; x < ViewPortWidth; x++)
        {
            var (c, colFg, colBg) = _visualByElevation[world.Cells[x, y]];
            colFg = fov.Cells[x, y] ? colFg : DefaultFg;
            colBg = fov.Cells[x, y] ? colBg : DefaultBg;
            _canvas.Set(
                X + WorldToViewportX(x) + _spaceForScaleLeft,
                Y + WorldToViewportY(y) + _spaceForScaleTop,
                c,
                colFg,
                colBg);
        }
    }

    private void RenderWorldByVisualMatrix(in FovComponent fov)
    {
        for (var y = _viewportPositionInWorldY; y < ViewPortHeight; y++)
        for (var x = _viewportPositionInWorldX; x < ViewPortWidth; x++)
        {
            var (c, colFg, _) = _visualByPosition[x, y];
            colFg = fov.Cells[x, y] ? colFg : DefaultFg;
            _canvas.Set(
                X + WorldToViewportX(x) + _spaceForScaleLeft,
                Y + WorldToViewportY(y) + _spaceForScaleTop,
                c,
                colFg,
                DefaultBg);
        }
    }

    private void RenderCoordinates()
    {
        for (var x = 0; x < _spaceForScaleLeft; x++)
            _canvas.Set(X + x, Y, Cp437.BlockFull, DefaultBg);

        // Horizontal
        // tick marks
        for (var x = _spaceForScaleLeft; x < Width; x++)
        {
            var worldX = ViewportToWorldX(x);
            var isTick = worldX > 0 && worldX % 10 == 0;
            var fg = isTick ? DefaultFg : DefaultBg;
            _canvas.Set(X + x, Y, Cp437.BlockFull, fg);
        }

        // tick labels
        for (var x = _spaceForScaleLeft; x < Width; x++)
        {
            var worldX = ViewportToWorldX(x);
            var isTick = worldX > 0 && worldX % 10 == 0;
            if (isTick)
                _canvas.Text(X + x, Y, Convert.ToString(x), false, DefaultBg, DefaultFg);
        }

        // Vertical
        // tick marks
        for (var y = _spaceForScaleTop; y < Height; y++)
        for (var x = 0; x < _spaceForScaleLeft; x++)
        {
            var worldY = ViewportToWorldY(y);
            var isTick = worldY > 0 && worldY % 5 == 0;
            var fg = isTick ? DefaultFg : DefaultBg;
            _canvas.Set(X + x, y, Cp437.BlockFull, fg);
        }

        // tick labels
        for (var y = _spaceForScaleTop; y < Height; y++)
        {
            var worldY = ViewportToWorldY(y);
            var isTick = worldY > 0 && worldY % 5 == 0;
            if (isTick)
                _canvas.Text(X, y, Convert.ToString(y), false, DefaultBg, DefaultFg);
        }
    }

    private void SetElevationLevelColorVisual()
    {
        _visualByElevation[0] = ('0', ConsoleColor.DarkBlue, DefaultBg);
        _visualByElevation[1] = ('1', ConsoleColor.Blue, DefaultBg);
        _visualByElevation[2] = ('2', ConsoleColor.DarkCyan, DefaultBg);
        _visualByElevation[3] = ('3', ConsoleColor.Cyan, DefaultBg);
        _visualByElevation[4] = ('4', ConsoleColor.Yellow, DefaultBg);
        _visualByElevation[5] = ('5', ConsoleColor.DarkGreen, DefaultBg);
        _visualByElevation[6] = ('6', ConsoleColor.Green, DefaultBg);
        _visualByElevation[7] = ('7', ConsoleColor.DarkYellow, DefaultBg);
        _visualByElevation[8] = ('8', ConsoleColor.DarkGray, DefaultBg);
        _visualByElevation[9] = ('9', ConsoleColor.Gray, DefaultBg);
    }

    private void SetElevationLevelMonochromeVisual()
    {
        _visualByElevation[0] = ('0', DefaultFg, DefaultBg);
        _visualByElevation[1] = ('1', DefaultFg, DefaultBg);
        _visualByElevation[2] = ('2', DefaultFg, DefaultBg);
        _visualByElevation[3] = ('3', DefaultFg, DefaultBg);
        _visualByElevation[4] = ('4', DefaultFg, DefaultBg);
        _visualByElevation[5] = ('5', DefaultFg, DefaultBg);
        _visualByElevation[6] = ('6', DefaultFg, DefaultBg);
        _visualByElevation[7] = ('7', DefaultFg, DefaultBg);
        _visualByElevation[8] = ('8', DefaultFg, DefaultBg);
        _visualByElevation[9] = ('9', DefaultFg, DefaultBg);
    }

    private void SetTerrainColorVisual()
    {
        _visualByElevation[0] = (Cp437.Tilde, ConsoleColor.DarkBlue, DefaultBg);
        _visualByElevation[1] = (Cp437.Tilde, ConsoleColor.Blue, DefaultBg);
        _visualByElevation[2] = (Cp437.Approximation, ConsoleColor.DarkCyan, DefaultBg);
        _visualByElevation[3] = (Cp437.Approximation, ConsoleColor.Cyan, DefaultBg);
        _visualByElevation[4] = (Cp437.SparseShade, ConsoleColor.Yellow, DefaultBg);
        _visualByElevation[5] = (Cp437.BoxDoubleUpHorizontal, ConsoleColor.DarkGreen, DefaultBg);
        _visualByElevation[6] = (Cp437.BoxUpHorizontal, ConsoleColor.Green, DefaultBg);
        _visualByElevation[7] = (Cp437.Intersection, ConsoleColor.DarkYellow, DefaultBg);
        _visualByElevation[8] = (Cp437.Caret, ConsoleColor.DarkGray, DefaultBg);
        _visualByElevation[9] = (Cp437.TriangleUp, ConsoleColor.Gray, DefaultBg);
    }

    private void SetTerrainMonochromeVisual()
    {
        _visualByElevation[0] = (Cp437.Tilde, DefaultFg, DefaultBg);
        _visualByElevation[1] = (Cp437.Tilde, DefaultFg, DefaultBg);
        _visualByElevation[2] = (Cp437.Approximation, DefaultFg, DefaultBg);
        _visualByElevation[3] = (Cp437.Approximation, DefaultFg, DefaultBg);
        _visualByElevation[4] = (Cp437.MediumShade, DefaultFg, DefaultBg);
        _visualByElevation[5] = (Cp437.BoxDoubleUpHorizontal, DefaultFg, DefaultBg);
        _visualByElevation[6] = (Cp437.BoxUpHorizontal, DefaultFg, DefaultBg);
        _visualByElevation[7] = (Cp437.Intersection, DefaultFg, DefaultBg);
        _visualByElevation[8] = (Cp437.Caret, DefaultFg, DefaultBg);
        _visualByElevation[9] = (Cp437.TriangleUp, DefaultFg, DefaultBg);
    }

    private void SetHeatmapColorVisual()
    {
        _visualByElevation[0] = (Cp437.DenseShade, ConsoleColor.DarkBlue, DefaultBg);
        _visualByElevation[1] = (Cp437.DenseShade, ConsoleColor.Blue, DefaultBg);
        _visualByElevation[2] = (Cp437.DenseShade, ConsoleColor.DarkCyan, DefaultBg);
        _visualByElevation[3] = (Cp437.DenseShade, ConsoleColor.Cyan, DefaultBg);
        _visualByElevation[4] = (Cp437.DenseShade, ConsoleColor.Yellow, DefaultBg);
        _visualByElevation[5] = (Cp437.DenseShade, ConsoleColor.DarkGreen, DefaultBg);
        _visualByElevation[6] = (Cp437.DenseShade, ConsoleColor.Green, DefaultBg);
        _visualByElevation[7] = (Cp437.DenseShade, ConsoleColor.DarkYellow, DefaultBg);
        _visualByElevation[8] = (Cp437.DenseShade, ConsoleColor.DarkGray, DefaultBg);
        _visualByElevation[9] = (Cp437.DenseShade, ConsoleColor.Gray, DefaultBg);

        /*
        _visualByElevation[0] = (Cp437.DenseShade, ConsoleColor.DarkBlue, DefaultBg);
        _visualByElevation[1] = (Cp437.DenseShade, ConsoleColor.Blue, DefaultBg);
        _visualByElevation[2] = (Cp437.DenseShade, ConsoleColor.DarkCyan, DefaultBg);
        _visualByElevation[3] = (Cp437.DenseShade, ConsoleColor.Cyan, DefaultBg);
        _visualByElevation[4] = (Cp437.DenseShade, ConsoleColor.Yellow, DefaultBg);
        _visualByElevation[5] = (Cp437.DenseShade, ConsoleColor.DarkGreen, DefaultBg);
        _visualByElevation[6] = (Cp437.DenseShade, ConsoleColor.Green, DefaultBg);
        _visualByElevation[7] = (Cp437.DenseShade, ConsoleColor.DarkYellow, DefaultBg);
        _visualByElevation[8] = (Cp437.DenseShade, ConsoleColor.DarkGray, DefaultBg);
        _visualByElevation[9] = (Cp437.DenseShade, ConsoleColor.Gray, DefaultBg);
        */
        /*
        _visualByElevation[0] = (Cp437.SparseShade, ConsoleColor.DarkBlue, DefaultBg);
        _visualByElevation[1] = (Cp437.SparseShade, ConsoleColor.Blue, DefaultBg);
        _visualByElevation[2] = (Cp437.SparseShade, ConsoleColor.DarkCyan, DefaultBg);
        _visualByElevation[3] = (Cp437.SparseShade, ConsoleColor.Cyan, DefaultBg);
        _visualByElevation[4] = (Cp437.SparseShade, ConsoleColor.Yellow, DefaultBg);
        _visualByElevation[5] = (Cp437.SparseShade, ConsoleColor.DarkGreen, DefaultBg);
        _visualByElevation[6] = (Cp437.SparseShade, ConsoleColor.Green, DefaultBg);
        _visualByElevation[7] = (Cp437.SparseShade, ConsoleColor.DarkYellow, DefaultBg);
        _visualByElevation[8] = (Cp437.SparseShade, ConsoleColor.DarkGray, DefaultBg);
        _visualByElevation[9] = (Cp437.SparseShade, ConsoleColor.Gray, DefaultBg);
        */
    }

    private void SetHeatmapMonochromeVisual()
    {
        _visualByElevation[0] = (Cp437.BlockFull, ConsoleColor.Black, ConsoleColor.Black);
        _visualByElevation[1] = (Cp437.SparseShade, ConsoleColor.DarkGray, ConsoleColor.Black);
        _visualByElevation[2] = (Cp437.MediumShade, ConsoleColor.DarkGray, ConsoleColor.Black);
        _visualByElevation[3] = (Cp437.DenseShade, ConsoleColor.DarkGray, ConsoleColor.Black);
        _visualByElevation[4] = (Cp437.SparseShade, ConsoleColor.Gray, ConsoleColor.DarkGray);
        _visualByElevation[5] = (Cp437.MediumShade, ConsoleColor.Gray, ConsoleColor.DarkGray);
        _visualByElevation[6] = (Cp437.DenseShade, ConsoleColor.Gray, ConsoleColor.DarkGray);
        _visualByElevation[7] = (Cp437.MediumShade, ConsoleColor.White, ConsoleColor.DarkGray);
        _visualByElevation[8] = (Cp437.DenseShade, ConsoleColor.White, ConsoleColor.DarkGray);
        _visualByElevation[9] = (Cp437.BlockFull, ConsoleColor.White, ConsoleColor.DarkGray);
    }

    private void SetWorldReliefVisual(WorldComponent world)
    {
        for (var y = 0; y < _worldHeight; y++)
        for (var x = 0; x < _worldWidth; x++)
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

            var colFg = MapRenderMode == MapRenderMode.ReliefMonochrome
                ? DefaultFg
                : _visualByElevation[world.Cells[x, y]].Item2;

            _visualByPosition[x, y] = (c, colFg, DefaultBg);
        }
    }

    private void SetWorldContourLines(WorldComponent world)
    {
        for (var y = 0; y < _worldHeight; y++)
        for (var x = 0; x < _worldWidth; x++)
        {
            var cell = world.Cells[x, y];
            if (cell < 3)
            {
                _visualByPosition[x, y] = (Cp437.WhiteSpace, DefaultFg, DefaultBg);
                continue;
            }

            var c = GetCharFromCliffs(cell, x, y, world);
            _visualByPosition[x, y].Item1 = c;
        }

        for (var y = 0; y < _worldHeight; y++)
        for (var x = 0; x < _worldWidth; x++)
        {
            var cell = world.Cells[x, y];
            if (cell < 3) continue;

            if (_visualByPosition[x, y].Item1 == 'X') continue;
            var c = GetCliffAdjacentChar(cell, x, y, world);

            var colFg = MapRenderMode == MapRenderMode.ContourMonochrome
                ? DefaultFg
                : _visualByElevation[world.Cells[x, y]].Item2;

            _visualByPosition[x, y] = (c, colFg, DefaultBg);
        }

        for (var y = 0; y < _worldHeight; y++)
        for (var x = 0; x < _worldWidth; x++)
        {
            var cell = world.Cells[x, y];
            if (cell < 3) continue;

            if (_visualByPosition[x, y].Item1 != 'X') continue;
            var c = GetCliffChar(cell, x, y, world);

            var colFg = MapRenderMode == MapRenderMode.ContourMonochrome
                ? DefaultFg
                : _visualByElevation[world.Cells[x, y]].Item2;

            _visualByPosition[x, y] = (c, colFg, DefaultBg);
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
        if (north == cell && _visualByPosition[x, y - 1].Item1 == Cp437.UpperX) b |= 0b_0000_1000;

        // east
        byte? east = IsInBounds(x + 1, y) ? world.Cells[x + 1, y] : null;
        if (east == cell && _visualByPosition[x + 1, y].Item1 == Cp437.UpperX) b |= 0b_0000_0100;

        // south
        byte? south = IsInBounds(x, y + 1) ? world.Cells[x, y + 1] : null;
        if (south == cell && _visualByPosition[x, y + 1].Item1 == Cp437.UpperX) b |= 0b_0000_0010;

        // west
        byte? west = IsInBounds(x - 1, y) ? world.Cells[x - 1, y] : null;
        if (west == cell && _visualByPosition[x - 1, y].Item1 == Cp437.UpperX) b |= 0b_0000_0001;

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
            10 => Cp437.BoxVertical, // 1010, north south
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
        if (north == cell && _visualByPosition[x, y - 1].Item1 != Cp437.WhiteSpace)
            b |= 0b_0000_1000;

        // east
        byte? east = IsInBounds(x + 1, y) ? world.Cells[x + 1, y] : null;
        if (east == cell && _visualByPosition[x + 1, y].Item1 != Cp437.WhiteSpace)
            b |= 0b_0000_0100;

        // south
        byte? south = IsInBounds(x, y + 1) ? world.Cells[x, y + 1] : null;
        if (south == cell && _visualByPosition[x, y + 1].Item1 != Cp437.WhiteSpace)
            b |= 0b_0000_0010;

        // west
        byte? west = IsInBounds(x - 1, y) ? world.Cells[x - 1, y] : null;
        if (west == cell && _visualByPosition[x - 1, y].Item1 != Cp437.WhiteSpace)
            b |= 0b_0000_0001;

        if (b == 0 && _visualByPosition[x, y].Item1 == Cp437.WhiteSpace) return Cp437.WhiteSpace;

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
            10 => Cp437.BoxVertical, // 1010, north south
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