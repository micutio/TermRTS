using System.Numerics;
using ConsoleRenderer;
using log4net;
using TermRTS.Io;

namespace TermRTS.Examples.Greenery;

public class Renderer : IRenderer, IEventSink
{
    #region Constructor
    
    public Renderer(int viewportWidth, int viewportHeight, int worldWidth, int worldHeight)
    {
        _canvas = new ConsoleCanvas().Render();
        _log = LogManager.GetLogger(GetType());
        _textbox = new TextBox();
        _viewportSize.X = viewportWidth;
        _viewportSize.Y = viewportHeight;
        _worldSize.X = worldWidth;
        _worldSize.Y = worldHeight;
        Console.CursorVisible = false;
    }
    
    #endregion
    
    #region IEventSink Members
    
    public void ProcessEvent(IEvent evt)
    {
        if (evt.Type() != EventType.KeyInput) return;
        
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
        
        _textbox.ProcessEvent(evt);
    }
    
    #endregion
    
    #region Public Fields
    
    private readonly Vector2 _viewportSize;
    private readonly Vector2 _worldSize;
    
    private Vector2 _cameraPos = new(0, 0);
    
    #endregion
    
    #region Private Fields
    
    private static readonly ConsoleColor DefaultBg = Console.BackgroundColor;
    private static readonly ConsoleColor DefaultFg = Console.ForegroundColor;
    private readonly ConsoleCanvas _canvas;
    private readonly ILog _log;
    
    // TODO: Find a more modular way of handling this.
    private readonly TextBox _textbox;
    
    #endregion
    
    #region IRenderer Members
    
    public void RenderComponents(
        in IStorage storage,
        double timeStepSizeMs,
        double howFarIntoNextFrameMs)
    {
        var worldComponent = storage
            .GetForType(typeof(WorldComponent))
            .First();
        if (worldComponent is WorldComponent world) RenderWorld(world);
        
        if (!_textbox.IsOngoingInput) return;
        
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
    
    private void RenderWorld(WorldComponent world)
    {
        // TODO: Only update whenever CameraPos changes.
        var minX = Convert.ToInt32(_cameraPos.X);
        var minY = Convert.ToInt32(_cameraPos.Y);
        var maxX = Convert.ToInt32(Math.Min(_cameraPos.X + _viewportSize.X, _worldSize.X));
        var maxY = Convert.ToInt32(Math.Min(_cameraPos.Y + _viewportSize.Y, _worldSize.Y));
        
        for (var y = minY; y < maxY; y++)
        for (var x = minX; x < maxX; x++)
        {
            // var (c, colFg, colBg) = GetElevationLevelColorVisual(world.Cells[x, y]);
            // var (c, colFg, colBg) = GetElevationLevelMonochromeVisual(world.Cells[x, y]);
            // var (c, colFg, colBg) = GetTerrainColorVisual(world.Cells[x, y]);
            var (c, colFg, colBg) = GetTerrainMonochromeVisual(world.Cells[x, y]);
            // var (c, colFg, colBg) = GetGrayScaleVisual(world.Cells[x, y]);
            _canvas.Set(x - minX, y - minY, c, colFg, colBg);
        }
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
    
    #region Private Members
    
    private void MoveCameraUp()
    {
        _cameraPos.Y = Math.Max(_cameraPos.Y - 1, 0);
    }
    
    private void MoveCameraDown()
    {
        _cameraPos.Y = Math.Max(0, Math.Min(_cameraPos.Y + 1, _worldSize.Y - _viewportSize.Y));
    }
    
    private void MoveCameraLeft()
    {
        _cameraPos.X = Math.Max(_cameraPos.X - 1, 0);
    }
    
    private void MoveCameraRight()
    {
        _cameraPos.X = Math.Max(0, Math.Min(_cameraPos.X + 1, _worldSize.X - _viewportSize.X));
    }
    
    private bool IsInCamera(float x, float y)
    {
        return x >= _cameraPos.X
               && y <= _cameraPos.X + _viewportSize.X
               && y >= _cameraPos.Y
               && y <= _cameraPos.Y + _viewportSize.Y;
    }
    
    private static (char, ConsoleColor, ConsoleColor) GetElevationLevelColorVisual(byte b)
    {
        return b switch
        {
            0 => ('0', ConsoleColor.DarkBlue, DefaultBg),
            1 => ('1', ConsoleColor.Blue, DefaultBg),
            2 => ('2', ConsoleColor.DarkCyan, DefaultBg),
            3 => ('3', ConsoleColor.Cyan, DefaultBg),
            4 => ('4', ConsoleColor.Yellow, DefaultBg),
            5 => ('5', ConsoleColor.DarkGreen, DefaultBg),
            6 => ('6', ConsoleColor.Green, DefaultBg),
            7 => ('7', ConsoleColor.DarkYellow, DefaultBg),
            8 => ('8', ConsoleColor.DarkGray, DefaultBg),
            _ => ('9', ConsoleColor.Gray, DefaultBg)
        };
    }
    
    private static (char, ConsoleColor, ConsoleColor) GetElevationLevelMonochromeVisual(byte b)
    {
        return b switch
        {
            0 => ('0', DefaultFg, DefaultBg),
            1 => ('1', DefaultFg, DefaultBg),
            2 => ('2', DefaultFg, DefaultBg),
            3 => ('3', DefaultFg, DefaultBg),
            4 => ('4', DefaultFg, DefaultBg),
            5 => ('5', DefaultFg, DefaultBg),
            6 => ('6', DefaultFg, DefaultBg),
            7 => ('7', DefaultFg, DefaultBg),
            8 => ('8', DefaultFg, DefaultBg),
            _ => ('9', DefaultFg, DefaultBg)
        };
    }
    
    private static (char, ConsoleColor, ConsoleColor) GetTerrainColorVisual(byte b)
    {
        return b switch
        {
            0 => (Cp437.TripleBar, ConsoleColor.DarkBlue, DefaultBg),
            1 => (Cp437.TripleBar, ConsoleColor.Blue, DefaultBg),
            2 => (Cp437.Approximation, ConsoleColor.DarkCyan, DefaultBg),
            3 => (Cp437.Tilde, ConsoleColor.Cyan, DefaultBg),
            4 => (Cp437.MediumShade, ConsoleColor.Yellow, DefaultBg),
            5 => (Cp437.BoxDoubleUpHorizontal, ConsoleColor.DarkGreen, DefaultBg),
            6 => (Cp437.BoxUpHorizontal, ConsoleColor.Green, DefaultBg),
            7 => (Cp437.Intersection, ConsoleColor.DarkYellow, DefaultBg),
            8 => (Cp437.Caret, ConsoleColor.DarkGray, DefaultBg),
            _ => (Cp437.TriangleUp, ConsoleColor.Gray, DefaultBg)
        };
    }
    
    private static (char, ConsoleColor, ConsoleColor) GetTerrainMonochromeVisual(byte b)
    {
        return b switch
        {
            0 => (Cp437.TripleBar, DefaultFg, DefaultBg),
            1 => (Cp437.TripleBar, DefaultFg, DefaultBg),
            2 => (Cp437.Approximation, DefaultFg, DefaultBg),
            3 => (Cp437.Tilde, DefaultFg, DefaultBg),
            4 => (Cp437.MediumShade, DefaultFg, DefaultBg),
            5 => (Cp437.BoxDoubleUpHorizontal, DefaultFg, DefaultBg),
            6 => (Cp437.BoxUpHorizontal, DefaultFg, DefaultBg),
            7 => (Cp437.Intersection, DefaultFg, DefaultBg),
            8 => (Cp437.Caret, DefaultFg, DefaultBg),
            _ => (Cp437.TriangleUp, DefaultFg, DefaultBg)
        };
    }
    
    private static (char, ConsoleColor, ConsoleColor) GetGrayScaleVisual(byte b)
    {
        return b switch
        {
            0 => (Cp437.BlockFull, ConsoleColor.Black, ConsoleColor.Black),
            1 => (Cp437.SparseShade, ConsoleColor.DarkGray, ConsoleColor.Black),
            2 => (Cp437.MediumShade, ConsoleColor.DarkGray, ConsoleColor.Black),
            3 => (Cp437.DenseShade, ConsoleColor.DarkGray, ConsoleColor.Black),
            4 => (Cp437.SparseShade, ConsoleColor.Gray, ConsoleColor.DarkGray),
            5 => (Cp437.MediumShade, ConsoleColor.Gray, ConsoleColor.DarkGray),
            6 => (Cp437.DenseShade, ConsoleColor.Gray, ConsoleColor.DarkGray),
            7 => (Cp437.MediumShade, ConsoleColor.White, ConsoleColor.DarkGray),
            8 => (Cp437.DenseShade, ConsoleColor.White, ConsoleColor.DarkGray),
            _ => (Cp437.BlockFull, ConsoleColor.White, ConsoleColor.DarkGray)
        };
    }
    
    #endregion
}