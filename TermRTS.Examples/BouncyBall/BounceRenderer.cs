using ConsoleRenderer;

namespace TermRTS.Examples.BouncyBall;

internal class BounceRenderer : TermRTS.IRenderer<BounceWorld, BounceComponentTypes>
{
    private ConsoleCanvas _canvas;

    public BounceRenderer()
    {
        _canvas = new ConsoleCanvas().Render();
        Console.CursorVisible = false;
    }

    public void RenderEntity(Dictionary<BounceComponentTypes, IComponent> entity, double howFarIntoNextFrameMs)
    {
        if (!entity.TryGetValue(BounceComponentTypes.Ball, out var ballComponent))
            return;

        var ball = (BounceBall)ballComponent;

        _canvas.Set((int)ball.Position.X, (int)ball.Position.Y, '*');

        // if (ball.Velocity == Vector2.Zero)
        //    return;

        // Console.WriteLine($"ball pos {ball.Position}, velocity {ball.Velocity})");
    }

    public void RenderWorld(BounceWorld world, double howFarIntoNextFrameMs, double timeStepSizeMs)
    {
        _canvas.Clear();
        _canvas.Text(0, 0, "Bounce World!");
    }

    public void FinalizeRender()
    {
        _canvas.Render();
    }
}
