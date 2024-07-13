using ConsoleRenderer;
using System.Numerics;

namespace TermRTS.Examples.Circuitry;

// TODO: Restore normal terminal colors after termination.
internal class Renderer : TermRTS.IRenderer<World, App.CircuitComponentTypes>
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

    public Renderer()
    {
        Console.CursorVisible = false;
        _canvas = new ConsoleCanvas().Render();
    }

    public void RenderWorld(World world, double timeStepSizeMs, double howFarIntoNextFrameMs)
    {
        _timePassed += (timeStepSizeMs + howFarIntoNextFrameMs);
        _fps += 1;
        if (_timePassed >= _lastSecond + 1000)
        {
            _lastSecond = _timePassed;
            _lastFps = _fps;
            _fps = 1;
        }

        _canvas.Clear();
        _canvas.Text(1, 1, $"Circuitry World  ~  FPS: {_lastFps}");
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

    private void RenderOutline(IEnumerable<(int, int, char)> outline)
    {
        foreach ((int x, int y, char c) tuple in outline.Where(o => IsInCamera(o.Item1, o.Item2)))
        {
            _canvas.Set(
                (int)(tuple.x - CameraPos.X),
                (int)(tuple.y - CameraPos.Y),
                tuple.c,
                ConsoleColor.Black);
        }
    }

    private void RenderWire(IReadOnlyList<(int, int, char)> outline, bool isActive, float progress)
    {
        var sparkIdx = (int)(outline.Count * progress);
        for (var i = 0; i < outline.Count; i++)
        {
            var (x, y, c) = outline[i];

            if (isActive && i == sparkIdx)
            {
                _canvas.Set(
                    (int)(x - CameraPos.X),
                    (int)(y - CameraPos.Y),
                    c,
                    ConsoleColor.Cyan,
                    DefaultBg);
            }
            else
            {
                _canvas.Set(
                    (int)(x - CameraPos.X),
                    (int)(y - CameraPos.Y),
                    c,
                    ConsoleColor.Black,
                    DefaultBg);
            }
        }
    }

    public void FinalizeRender()
    {
        _canvas.Render();
    }

    private bool IsInCamera(float x, float y)
    {
        return (x >= CameraPos.X && y <= CameraSize.X - CameraPos.X)
               && (y >= CameraPos.Y && y <= CameraSize.Y - CameraPos.Y);
    }

    public void Shutdown()
    {
        _canvas.Clear();
        _canvas.Render();
    }
}
