using ConsoleRenderer;
using System.Numerics;

namespace TermRTS.Examples.Circuitry;

internal class Renderer : TermRTS.IRenderer<World, App.CircuitComponentTypes>
{
    private readonly ConsoleCanvas _canvas;
    public Vector2 Size = new(Console.WindowWidth, Console.WindowHeight);
    public Vector2 CameraPos = new(0, 0);
    public Vector2 CameraSize = new(Console.WindowWidth, Console.WindowHeight);

    public Renderer()
    {
        Console.CursorVisible = false;
        _canvas = new ConsoleCanvas().Render();
    }

    public void RenderWorld(World world, double howFarIntoNextFrameMs)
    {
        _canvas.Clear();
        _canvas.Text(1, 1, "Circuitry World");
    }

    public void RenderEntity(Dictionary<App.CircuitComponentTypes, IComponent> entity, double howFarIntoNextFrameMs)
    {
        if (entity.TryGetValue(App.CircuitComponentTypes.Chip, out var chipComponent))
        {
            RenderOutline(((App.Chip)chipComponent).Outline);
            return;
        }

        if (entity.TryGetValue(App.CircuitComponentTypes.Wire, out var wireComponent))
        {
            RenderOutline(((App.Wire)wireComponent).Outline);
        }
    }

    private void RenderOutline(IEnumerable<(int, int, char)> outline)
    {
        foreach ((int x, int y, char c) tuple in outline.Where(o => IsInCamera(o.Item1, o.Item2)))
        {
            _canvas.Set((int)(tuple.x - CameraPos.X), (int)(tuple.y - CameraPos.Y), tuple.c);
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
}