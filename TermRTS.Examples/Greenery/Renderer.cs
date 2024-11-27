using System.Numerics;
using ConsoleRenderer;
using log4net;
using TermRTS.Events;
using TermRTS.Io;

namespace TermRTS.Examples.Greenery;

public class Renderer : IRenderer, IEventSink
{
    #region Private Fields
    
    private static readonly ConsoleColor DefaultBg = Console.BackgroundColor;
    private static readonly ConsoleColor DefaultFg = Console.ForegroundColor;
    private readonly ConsoleCanvas _canvas;
    private readonly (char, ConsoleColor, ConsoleColor)[] _visuals;
    private readonly ILog _log;
    
    private readonly Vector2 _viewportSize;
    private readonly Vector2 _worldSize;
    
    // TODO: Find a more modular way of handling this.
    private readonly TextBox _textbox;
    
    private string _profileOutput;
    private double _timePassedMs;
    
    private int _cameraPosX;
    private int _cameraPosY;
    
    // Keep track of visible world coordinates
    private int _maxX;
    private int _maxY;
    
    #endregion
    
    #region Constructor
    
    public Renderer(int viewportWidth, int viewportHeight, int worldWidth, int worldHeight)
    {
        _canvas = new ConsoleCanvas().Render();
        _visuals = new (char, ConsoleColor, ConsoleColor)[10];
        _log = LogManager.GetLogger(GetType());
        _textbox = new TextBox();
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
    
    #region IRenderer Members
    
    public void RenderComponents(
        in IStorage storage,
        double timeStepSizeMs,
        double howFarIntoNextFramePercent)
    {
        // Step 1: Render world
        foreach (var worldComponent in storage.GetForType(typeof(WorldComponent)))
            if (worldComponent is WorldComponent world)
                RenderWorld(world);
        
        // Step 2: Render profiling info on top of the world
#if DEBUG
        // RenderInfo(timeStepSizeMs, howFarIntoNextFramePercent);
#endif
        
        if (!_textbox.IsOngoingInput) return;
        
        // Step 3: Render textbox if its contents have changed.
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
        for (var y = CameraPosY; y < _maxY; y++)
        for (var x = CameraPosX; x < _maxX; x++)
        {
            var (c, colFg, colBg) = _visuals[world.Cells[x, y]];
            _canvas.Set(x - CameraPosX, y - CameraPosY, c, colFg, colBg);
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
               && y <= CameraPosX + _viewportSize.X
               && y >= CameraPosY
               && y <= CameraPosY + _viewportSize.Y;
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
    
    private void SetElevationLevelColorVisual()
    {
        _visuals[0] = ('0', ConsoleColor.DarkBlue, DefaultBg);
        _visuals[1] = ('1', ConsoleColor.Blue, DefaultBg);
        _visuals[2] = ('2', ConsoleColor.DarkCyan, DefaultBg);
        _visuals[3] = ('3', ConsoleColor.Cyan, DefaultBg);
        _visuals[4] = ('4', ConsoleColor.Yellow, DefaultBg);
        _visuals[5] = ('5', ConsoleColor.DarkGreen, DefaultBg);
        _visuals[6] = ('6', ConsoleColor.Green, DefaultBg);
        _visuals[7] = ('7', ConsoleColor.DarkYellow, DefaultBg);
        _visuals[8] = ('8', ConsoleColor.DarkGray, DefaultBg);
        _visuals[9] = ('9', ConsoleColor.Gray, DefaultBg);
    }
    
    private void SetElevationLevelMonochromeVisual()
    {
        _visuals[0] = ('0', DefaultFg, DefaultBg);
        _visuals[1] = ('1', DefaultFg, DefaultBg);
        _visuals[2] = ('2', DefaultFg, DefaultBg);
        _visuals[3] = ('3', DefaultFg, DefaultBg);
        _visuals[4] = ('4', DefaultFg, DefaultBg);
        _visuals[5] = ('5', DefaultFg, DefaultBg);
        _visuals[6] = ('6', DefaultFg, DefaultBg);
        _visuals[7] = ('7', DefaultFg, DefaultBg);
        _visuals[8] = ('8', DefaultFg, DefaultBg);
        _visuals[9] = ('9', DefaultFg, DefaultBg);
    }
    
    private void SetTerrainColorVisual()
    {
        _visuals[0] = (Cp437.TripleBar, ConsoleColor.DarkBlue, DefaultBg);
        _visuals[1] = (Cp437.TripleBar, ConsoleColor.Blue, DefaultBg);
        _visuals[2] = (Cp437.Approximation, ConsoleColor.DarkCyan, DefaultBg);
        _visuals[3] = (Cp437.Tilde, ConsoleColor.Cyan, DefaultBg);
        _visuals[4] = (Cp437.MediumShade, ConsoleColor.Yellow, DefaultBg);
        _visuals[5] = (Cp437.BoxDoubleUpHorizontal, ConsoleColor.DarkGreen, DefaultBg);
        _visuals[6] = (Cp437.BoxUpHorizontal, ConsoleColor.Green, DefaultBg);
        _visuals[7] = (Cp437.Intersection, ConsoleColor.DarkYellow, DefaultBg);
        _visuals[8] = (Cp437.Caret, ConsoleColor.DarkGray, DefaultBg);
        _visuals[9] = (Cp437.TriangleUp, ConsoleColor.Gray, DefaultBg);
    }
    
    private void SetTerrainMonochromeVisual()
    {
        _visuals[0] = (Cp437.TripleBar, DefaultFg, DefaultBg);
        _visuals[1] = (Cp437.TripleBar, DefaultFg, DefaultBg);
        _visuals[2] = (Cp437.Approximation, DefaultFg, DefaultBg);
        _visuals[3] = (Cp437.Tilde, DefaultFg, DefaultBg);
        _visuals[4] = (Cp437.MediumShade, DefaultFg, DefaultBg);
        _visuals[5] = (Cp437.BoxDoubleUpHorizontal, DefaultFg, DefaultBg);
        _visuals[6] = (Cp437.BoxUpHorizontal, DefaultFg, DefaultBg);
        _visuals[7] = (Cp437.Intersection, DefaultFg, DefaultBg);
        _visuals[8] = (Cp437.Caret, DefaultFg, DefaultBg);
        _visuals[9] = (Cp437.TriangleUp, DefaultFg, DefaultBg);
    }
    
    private void SetGrayScaleVisual()
    {
        _visuals[0] = (Cp437.BlockFull, ConsoleColor.Black, ConsoleColor.Black);
        _visuals[1] = (Cp437.SparseShade, ConsoleColor.DarkGray, ConsoleColor.Black);
        _visuals[2] = (Cp437.MediumShade, ConsoleColor.DarkGray, ConsoleColor.Black);
        _visuals[3] = (Cp437.DenseShade, ConsoleColor.DarkGray, ConsoleColor.Black);
        _visuals[4] = (Cp437.SparseShade, ConsoleColor.Gray, ConsoleColor.DarkGray);
        _visuals[5] = (Cp437.MediumShade, ConsoleColor.Gray, ConsoleColor.DarkGray);
        _visuals[6] = (Cp437.DenseShade, ConsoleColor.Gray, ConsoleColor.DarkGray);
        _visuals[7] = (Cp437.MediumShade, ConsoleColor.White, ConsoleColor.DarkGray);
        _visuals[8] = (Cp437.DenseShade, ConsoleColor.White, ConsoleColor.DarkGray);
        _visuals[9] = (Cp437.BlockFull, ConsoleColor.White, ConsoleColor.DarkGray);
    }
    
    #endregion
}