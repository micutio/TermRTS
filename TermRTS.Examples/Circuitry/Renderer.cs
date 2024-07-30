using ConsoleRenderer;
using System.Numerics;

namespace TermRTS.Examples.Circuitry;

internal class Renderer : IRenderer<World, App.CircuitComponentTypes>, IEventSink
{
    private readonly ConsoleCanvas _canvas;
    public Vector2 Size = new(Console.WindowWidth, Console.WindowHeight);
    public Vector2 CameraPos = new(0, 0);
    public Vector2 CameraSize = new(Console.WindowWidth, Console.WindowHeight);

    private static readonly ConsoleColor DefaultBg = Console.BackgroundColor;
    private static readonly ConsoleColor DefaultFg = Console.ForegroundColor;

    private double _timePassed;
    private double _lastSecond;
    private int _fps;
    private int _lastFps;
    private string _profileOutput;

    public Renderer()
    {
        Console.CursorVisible = false;
        _canvas = new ConsoleCanvas().Render();
        _profileOutput = string.Empty;
    }

    public void RenderWorld(World world, double timeStepSizeMs, double howFarIntoNextFrameMs)
    {
        _timePassed += timeStepSizeMs + howFarIntoNextFrameMs;
        _fps += 1;
        if (_timePassed >= _lastSecond + 1000)
        {
            _lastSecond = _timePassed;
            _lastFps = _fps;
            _fps = 1;
        }

        _canvas.Clear();

#if DEBUG
        var debugStr = string.IsNullOrEmpty(_profileOutput)
            ? ""
            : $" ~ {_profileOutput}";
        _canvas.Text(1, 0, $"Circuitry World  ~  FPS: {_lastFps} {debugStr}");
#endif
    }

    public void RenderEntity(
        Dictionary<App.CircuitComponentTypes, IComponent> entity,
        double howFarIntoNextFrameMs)
    {
        if (entity.TryGetValue(App.CircuitComponentTypes.Chip, out var chipComponent))
        {
            RenderOutline(((App.Chip)chipComponent).Outline);
            return;
        }

        if (!entity.TryGetValue(App.CircuitComponentTypes.Bus, out var busComponent))
            return;

        var bus = (App.Bus)busComponent;
        var progress = bus.IsForward ? bus.Progress : 1.0f - bus.Progress;
        bus
            .Connections
            .ForEach(wire => RenderWire(wire.Outline, bus.IsActive, progress));
    }

    private void RenderOutline(App.Cell[] outline)
    {
        foreach (var cell in outline.Where(c => IsInCamera(c.X, c.Y)))
            _canvas.Set(
                (int)(cell.X - CameraPos.X),
                (int)(cell.Y - CameraPos.Y),
                cell.C,
                ConsoleColor.Black);
    }

    private void RenderWire(App.Cell[] outline, bool isActive, float progress)
    {
        var sparkIdx = (int)(outline.Length * progress);
        for (var i = 0; i < outline.Length; i++)
        {
            var (x, y, c) = outline[i];

            if (isActive && i == sparkIdx)
                _canvas.Set(
                    (int)(x - CameraPos.X),
                    (int)(y - CameraPos.Y),
                    c,
                    ConsoleColor.Blue,
                    DefaultBg);
            else
                _canvas.Set(
                    (int)(x - CameraPos.X),
                    (int)(y - CameraPos.Y),
                    c,
                    ConsoleColor.Black,
                    DefaultBg);
        }
    }

    public void FinalizeRender()
    {
        _canvas.Render();
    }

    private bool IsInCamera(float x, float y)
    {
        return x >= CameraPos.X && y <= CameraSize.X - CameraPos.X
                                && y >= CameraPos.Y && y <= CameraSize.Y - CameraPos.Y;
    }

    public void Shutdown()
    {
        Console.ResetColor();
    }

    #region IEventSink Members

    /// <inheritdoc/>>
    public void ProcessEvent(IEvent evt)
    {
        _profileOutput = evt.Type() switch
        {
            EventType.Profile => ((ProfileEvent)evt).ProfileInfo,
            _ => _profileOutput
        };
    }

    #endregion
}