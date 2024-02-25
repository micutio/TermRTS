using System.Numerics;

using ConsoleRenderer;

namespace TermRTS.Examples.BouncyBall;

internal class BounceRenderer : TermRTS.IRenderer<BounceWorld, BounceComponents>
{
    private ConsoleCanvas _canvas;

    public BounceRenderer()
    {
        _canvas = new ConsoleCanvas().Render();
        Console.CursorVisible = false;
    }

    public void RenderEntity(Dictionary<BounceComponents, IComponent> entity, double howFarIntoNextFrameMs)
    {
        if (!entity.TryGetValue(BounceComponents.Ball, out var ballComponent))
            return;

        var ball = (BounceBall)ballComponent;

        _canvas.Set((int)ball.Position.X, (int)ball.Position.Y, '*');

        // if (ball.Velocity == Vector2.Zero)
        //    return;

        // Console.WriteLine($"ball pos {ball.Position}, velocity {ball.Velocity})");
    }

    public void RenderWorld(BounceWorld world, double howFarIntoNextFrameMs)
    {
        _canvas.Clear();
        _canvas.Text(0, 0, "Bounce World!");
    }

    public void FinalizeRender()
    {
        _canvas.Render();
    }
}
