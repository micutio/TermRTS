using ConsoleRenderer;

namespace TermRTS.Examples.BouncyBall;

internal class BounceRenderer : IRenderer
{
    private readonly ConsoleCanvas _canvas;

    public BounceRenderer()
    {
        _canvas = new ConsoleCanvas().Render();
        Console.CursorVisible = false;
    }

    #region IRenderer Members

    public void RenderComponents(in IStorage storage, double timeStepSizeMs,
        double howFarIntoNextFramePercent)
    {
        _canvas.Clear();
        var ballComponents = storage.GetAllForType<BounceBall>();

        foreach (var ballPos in ballComponents.Select(ball => ball.Position))
            _canvas.Set(Convert.ToInt32(ballPos.X), Convert.ToInt32(ballPos.Y));

        // if (ball.Velocity == Vector2.Zero)
        //    return;
    }

    public void FinalizeRender()
    {
        _canvas.Render();
    }

    public void Shutdown()
    {
        _canvas.Clear();
        _canvas.Render();
    }

    #endregion
}