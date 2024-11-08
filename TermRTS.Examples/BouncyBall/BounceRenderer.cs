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
    
    public void RenderComponents(in IStorage storage, double timeStepSizeMs, double howFarIntoNextFrameMs)
    {
        _canvas.Clear();
        var ballComponents = storage.GetForType(typeof(BounceBall));
        
        foreach (var ballComponent in ballComponents)
        {
            var ball = (BounceBall)ballComponent;
            
            _canvas.Set(Convert.ToInt32(ball.Position.X), Convert.ToInt32(ball.Position.Y));
        }
        
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
}