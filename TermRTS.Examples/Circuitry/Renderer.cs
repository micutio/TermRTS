using System.Numerics;
using ConsoleRenderer;

namespace TermRTS.Examples.Circuitry;

internal class Renderer : IRenderer, IEventSink
{
    private static readonly ConsoleColor DefaultBg = Console.BackgroundColor;
    private static readonly ConsoleColor DefaultFg = Console.ForegroundColor;
    private readonly ConsoleCanvas _canvas;
    private int _fps;
    private int _lastFps;
    private double _lastSecond;
    private string _profileOutput;
    
    private double _timePassedMs;
    public Vector2 CameraPos = new(0, 0);
    public Vector2 CameraSize = new(Console.WindowWidth, Console.WindowHeight);
    public Vector2 Size = new(Console.WindowWidth, Console.WindowHeight);
    
    #region Constructor
    
    public Renderer()
    {
        Console.CursorVisible = false;
        _canvas = new ConsoleCanvas().Render();
        _profileOutput = string.Empty;
    }
    
    #endregion
    
    #region IEventSink Members
    
    /// <inheritdoc />
    /// >
    public void ProcessEvent(IEvent evt)
    {
        _profileOutput = evt.Type() switch
        {
            EventType.Profile => ((ProfileEvent)evt).ProfileInfo,
            _ => _profileOutput
        };
    }
    
    #endregion
    
    private void RenderInfo(double timeStepSizeMs, double howFarIntoNextFrameMs)
    {
        _timePassedMs += timeStepSizeMs + howFarIntoNextFrameMs;
        _fps += 1;
        if (_timePassedMs >= _lastSecond + 1000)
        {
            _lastSecond = _timePassedMs;
            _lastFps = _fps;
            _fps = 1;
        }
        
        _canvas.Clear();
        
#if DEBUG
        var debugStr = string.IsNullOrEmpty(_profileOutput) ? "" : $" ~ {_profileOutput}";
        var sec = (int)Math.Floor(_timePassedMs / 1000);
        var min = (int)Math.Floor(_timePassedMs / (1000 * 60));
        var hr = (int)Math.Floor(_timePassedMs / (1000 * 60 * 60));
        _canvas.Text(1, 0, $"Circuitry World ~ {hr}:{min}:{sec} ~ FPS: {_lastFps} {debugStr}");
#endif
    }
    
    private void RenderOutline(IEnumerable<App.Cell> outline)
    {
        foreach (var cell in outline.Where(c => IsInCamera(c.X, c.Y)))
            _canvas.Set(
                (int)(cell.X - CameraPos.X),
                (int)(cell.Y - CameraPos.Y),
                cell.C,
                ConsoleColor.Black);
    }
    
    private void RenderWire(IReadOnlyList<App.Cell> outline, bool isActive, float progress)
    {
        var sparkIdx = (int)(outline.Count * progress);
        for (var i = 0; i < outline.Count; i++)
        {
            var (x, y, c) = outline[i];
            var deltaX = (int)(x - CameraPos.X);
            var deltaY = (int)(y - CameraPos.Y);
            
            if (isActive && i == sparkIdx)
                _canvas.Set(deltaX, deltaY, c, ConsoleColor.Blue, DefaultBg);
            else
                _canvas.Set(deltaX, deltaY, c, ConsoleColor.Black, DefaultBg);
        }
    }
    
    private bool IsInCamera(float x, float y)
    {
        return x >= CameraPos.X
               && y <= CameraSize.X - CameraPos.X
               && y >= CameraPos.Y
               && y <= CameraSize.Y - CameraPos.Y;
    }
    
    #region Public Methods
    
    public void RenderComponents(in IStorage storage, double timeStepSizeMs, double howFarIntoNextFrameMs)
    {
        RenderInfo(timeStepSizeMs, howFarIntoNextFrameMs);
        
        foreach (var chip in storage.GetForType(typeof(App.Chip)).Cast<App.Chip>()) RenderOutline(chip.Outline);
        
        foreach (var bus in storage.GetForType(typeof(App.Bus)).Cast<App.Bus>())
        {
            var progress = bus.IsForward ? bus.Progress : 1.0f - bus.Progress;
            bus
                .Connections
                .ForEach(wire => RenderWire(wire.Outline, bus.IsActive, progress));
        }
    }
    
    public void FinalizeRender()
    {
        _canvas.Render();
    }
    
    public void Shutdown()
    {
        Console.ResetColor();
    }
    
    #endregion
}