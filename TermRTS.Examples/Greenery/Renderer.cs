using System.Numerics;
using ConsoleRenderer;
using log4net;
using TermRTS.Event;
using TermRTS.Events;
using TermRTS.Examples.Greenery.Event;
using TermRTS.Examples.Greenery.Ui;
using TermRTS.Io;

namespace TermRTS.Examples.Greenery;

public enum RenderMode
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

public class Renderer : IRenderer, IEventSink
{
    #region Constructor
    
    public Renderer(int viewportWidth, int viewportHeight, int worldWidth, int worldHeight, TextBox textbox)
    {
        _canvas = new ConsoleCanvas().Render();
        _visualByElevation = new (char, ConsoleColor, ConsoleColor)[10];
        _visualByPosition = new (char, ConsoleColor, ConsoleColor)[worldWidth, worldHeight];
        _log = LogManager.GetLogger(GetType());
        _textbox = textbox;
        _viewportSize.X = viewportWidth;
        _viewportSize.Y = viewportHeight;
        _worldSize.X = worldWidth;
        _worldSize.Y = worldHeight;
        _profileOutput = string.Empty;
        
        CameraPosX = 0;
        CameraPosY = 0;
        
        SetElevationLevelColorVisual();
        Console.CursorVisible = false;
    }
    
    #endregion
    
    #region Properties
    
    private RenderMode RenderMode
    {
        get => _renderMode;
        set
        {
            _renderMode = value;
            switch (_renderMode)
            {
                case RenderMode.ElevationColor:
                    SetElevationLevelColorVisual();
                    break;
                case RenderMode.ElevationMonochrome:
                    SetElevationLevelMonochromeVisual();
                    break;
                case RenderMode.HeatMapColor:
                    SetHeatmapColorVisual();
                    break;
                case RenderMode.HeatMapMonochrome:
                    SetHeatmapMonochromeVisual();
                    break;
                case RenderMode.TerrainColor:
                    SetTerrainColorVisual();
                    break;
                case RenderMode.ReliefColor:
                case RenderMode.ContourColor:
                    SetTerrainColorVisual();
                    _initVisualMatrix = true;
                    break;
                case RenderMode.TerrainMonochrome:
                    SetTerrainMonochromeVisual();
                    break;
                case RenderMode.ReliefMonochrome:
                case RenderMode.ContourMonochrome:
                    SetTerrainMonochromeVisual();
                    _initVisualMatrix = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
    
    #endregion
    
    #region IEventSink Members
    
    public void ProcessEvent(IEvent evt)
    {
#if DEBUG
        _profileOutput = evt.Type() switch
        {
            EventType.Profile => ((ProfileEvent)evt).ProfileInfo,
            _ => _profileOutput
        };
#endif
        
        if (!_textbox.IsOngoingInput && evt.Type() == EventType.KeyInput)
        {
            var keyEvent = (KeyInputEvent)evt;
            switch (keyEvent.Info.Key)
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
            }
        }
        
        if (evt.Type() == EventType.Custom && evt is RenderOptionEvent roe) RenderMode = roe.RenderMode;
    }
    
    #endregion
    
    #region Private Fields
    
    private static readonly ConsoleColor DefaultBg = Console.BackgroundColor;
    private static readonly ConsoleColor DefaultFg = Console.ForegroundColor;
    private readonly ConsoleCanvas _canvas;
    private readonly (char, ConsoleColor, ConsoleColor)[] _visualByElevation;
    private readonly (char, ConsoleColor, ConsoleColor)[,] _visualByPosition;
    private readonly ILog _log;
    
    private readonly Vector2 _viewportSize;
    private readonly Vector2 _worldSize;
    
    // TODO: Find a more modular way of handling this.
    private readonly TextBox _textbox;
    
    private RenderMode _renderMode = RenderMode.ContourColor;
    private bool _initVisualMatrix = true;
    
    private string _profileOutput;
    private double _timePassedMs;
    
    private int _cameraPosX;
    private int _cameraPosY;
    
    // Keep track of visible world coordinates
    private int _maxX;
    private int _maxY;
    
    #endregion
    
    #region IRenderer Members
    
    public void RenderComponents(
        in IStorage storage,
        double timeStepSizeMs,
        double howFarIntoNextFramePercent)
    {
        // Step 1: Render world
        storage.GetForType(typeof(WorldComponent), out var worldComponents);
        foreach (var worldComponent in worldComponents)
            if (worldComponent is WorldComponent world)
                switch (RenderMode)
                {
                    case RenderMode.ElevationColor:
                    case RenderMode.ElevationMonochrome:
                    case RenderMode.HeatMapColor:
                    case RenderMode.HeatMapMonochrome:
                    case RenderMode.TerrainColor:
                    case RenderMode.TerrainMonochrome:
                        RenderWorldByElevationVisuals(world);
                        break;
                    case RenderMode.ReliefColor:
                    case RenderMode.ReliefMonochrome:
                        if (_initVisualMatrix)
                        {
                            SetWorldReliefVisual(world);
                            _initVisualMatrix = false;
                        }
                        
                        RenderWorldByVisualMatrix();
                        break;
                    case RenderMode.ContourColor:
                    case RenderMode.ContourMonochrome:
                        if (_initVisualMatrix)
                        {
                            SetWorldContourLines(world);
                            _initVisualMatrix = false;
                        }
                        
                        RenderWorldByVisualMatrix();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
        
        // Step 2: Render profiling info on top of the world
#if DEBUG
        RenderInfo(timeStepSizeMs, howFarIntoNextFramePercent);
#endif
        
        // Step 3: Render textbox if its contents have changed.
        if (_textbox.IsOngoingInput) RenderTextbox();
    }
    
    public void FinalizeRender()
    {
        _canvas.Render();
    }
    
    public void Shutdown()
    {
        Console.ResetColor();
        _log.Info("Shutting down renderer.");
    }
    
    #endregion
    
    #region Properties
    
    private int CameraPosX
    {
        get => _cameraPosX;
        set
        {
            _cameraPosX = value;
            _maxX = Convert.ToInt32(Math.Min(_cameraPosX + _viewportSize.X, _worldSize.X));
        }
    }
    
    private int CameraPosY
    {
        get => _cameraPosY;
        set
        {
            _cameraPosY = value;
            _maxY = Convert.ToInt32(Math.Min(_cameraPosY + _viewportSize.Y, _worldSize.Y));
        }
    }
    
    #endregion
    
    #region Private Members
    
    private void MoveCameraUp()
    {
        CameraPosY = Math.Max(CameraPosY - 1, 0);
    }
    
    private void MoveCameraDown()
    {
        CameraPosY =
            Convert.ToInt32(Math.Clamp(CameraPosY + 1, 0, _worldSize.Y - _viewportSize.Y));
    }
    
    private void MoveCameraLeft()
    {
        CameraPosX = Math.Max(CameraPosX - 1, 0);
    }
    
    private void MoveCameraRight()
    {
        CameraPosX =
            Convert.ToInt32(Math.Clamp(CameraPosX + 1, 0, _worldSize.X - _viewportSize.X));
    }
    
    private bool IsInCamera(float x, float y)
    {
        return x >= CameraPosX
               && x <= CameraPosX + _viewportSize.X
               && y >= CameraPosY
               && y <= CameraPosY + _viewportSize.Y;
    }
    
    private bool IsInBounds(float x, float y)
    {
        return x >= 0
               && x < _worldSize.X
               && y >= 0
               && y < _worldSize.Y;
    }
    
    private void RenderWorldByElevationVisuals(WorldComponent world)
    {
        for (var y = CameraPosY; y < _maxY; y++)
        for (var x = CameraPosX; x < _maxX; x++)
        {
            var (c, colFg, colBg) = _visualByElevation[world.Cells[x, y]];
            _canvas.Set(x - CameraPosX, y - CameraPosY, c, colFg, colBg);
        }
    }
    
    private void RenderWorldByVisualMatrix()
    {
        for (var y = CameraPosY; y < _maxY; y++)
        for (var x = CameraPosX; x < _maxX; x++)
        {
            var (c, colFg, _) = _visualByPosition[x, y];
            _canvas.Set(x - CameraPosX, y - CameraPosY, c, colFg, DefaultBg);
        }
    }
    
    private void RenderInfo(double timeStepSizeMs, double howFarIntoNextFramePercent)
    {
        _timePassedMs += timeStepSizeMs + timeStepSizeMs * howFarIntoNextFramePercent;
        
        var debugStr = string.IsNullOrEmpty(_profileOutput)
            ? string.Empty
            : _profileOutput;
        var sec = (int)Math.Floor(_timePassedMs / 1000) % 60;
        var min = (int)Math.Floor(_timePassedMs / (1000 * 60)) % 60;
        var hr = (int)Math.Floor(_timePassedMs / (1000 * 60 * 60)) % 24;
        _canvas.Text(1, 0, $"Greenery | {hr:D2}:{min:D2}:{sec:D2} | {debugStr}");
    }
    
    private void RenderTextbox()
    {
        var x = Convert.ToInt32(_viewportSize.X - 1);
        var y = Convert.ToInt32(_viewportSize.Y - 1);
        var fg = DefaultFg;
        var bg = DefaultBg;
        
        for (var i = 0; i < x; i += 1)
            _canvas.Set(i, y, ' ', bg, fg);
        
        _canvas.Set(0, y, '>', bg, fg);
        _canvas.Set(1, y, ' ', bg, fg);
        
        var input = _textbox.GetCurrentInput();
        for (var i = 0; i < input.Count; i += 1)
        {
            var c = input[i];
            _canvas.Set(2 + i, y, c, bg, fg);
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
        for (var y = 0; y < _worldSize.Y; y++)
        for (var x = 0; x < _worldSize.X; x++)
        {
            char c;
            if (x == CameraPosX)
                c = '_';
            else if (IsInBounds(x - 1, y) && world.Cells[x - 1, y] < world.Cells[x, y] && world.Cells[x, y] > 3)
                c = Cp437.Slash;
            else if (IsInBounds(x - 1, y) && world.Cells[x - 1, y] > world.Cells[x, y] && world.Cells[x - 1, y] > 3)
                c = Cp437.BackSlash;
            else if (world.Cells[x, y] <= 3)
                c = '~';
            else
                c = ' ';
            
            var colFg = RenderMode == RenderMode.ReliefMonochrome
                ? DefaultFg
                : _visualByElevation[world.Cells[x, y]].Item2;
            
            _visualByPosition[x, y] = (c, colFg, DefaultBg);
        }
    }
    
    private void SetWorldContourLines(WorldComponent world)
    {
        for (var y = 0; y < _worldSize.Y; y++)
        for (var x = 0; x < _worldSize.X; x++)
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
        
        for (var y = 0; y < _worldSize.Y; y++)
        for (var x = 0; x < _worldSize.X; x++)
        {
            var cell = world.Cells[x, y];
            if (cell < 3) continue;
            
            if (_visualByPosition[x, y].Item1 == 'X') continue;
            var c = GetCliffAdjacentChar(cell, x, y, world);
            
            var colFg = RenderMode == RenderMode.ContourMonochrome
                ? DefaultFg
                : _visualByElevation[world.Cells[x, y]].Item2;
            
            _visualByPosition[x, y] = (c, colFg, DefaultBg);
        }
        
        for (var y = 0; y < _worldSize.Y; y++)
        for (var x = 0; x < _worldSize.X; x++)
        {
            var cell = world.Cells[x, y];
            if (cell < 3) continue;
            
            if (_visualByPosition[x, y].Item1 != 'X') continue;
            var c = GetCliffChar(cell, x, y, world);
            
            var colFg = RenderMode == RenderMode.ContourMonochrome
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
        if (north == cell && _visualByPosition[x, y - 1].Item1 != Cp437.WhiteSpace) b |= 0b_0000_1000;
        
        // east
        byte? east = IsInBounds(x + 1, y) ? world.Cells[x + 1, y] : null;
        if (east == cell && _visualByPosition[x + 1, y].Item1 != Cp437.WhiteSpace) b |= 0b_0000_0100;
        
        // south
        byte? south = IsInBounds(x, y + 1) ? world.Cells[x, y + 1] : null;
        if (south == cell && _visualByPosition[x, y + 1].Item1 != Cp437.WhiteSpace) b |= 0b_0000_0010;
        
        // west
        byte? west = IsInBounds(x - 1, y) ? world.Cells[x - 1, y] : null;
        if (west == cell && _visualByPosition[x - 1, y].Item1 != Cp437.WhiteSpace) b |= 0b_0000_0001;
        
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