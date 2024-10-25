using ConsoleRenderer;
using log4net;
using System.Numerics;

namespace TermRTS.Examples.Greenery;

public class Renderer : IRenderer, IEventSink
{
    #region Public Fields
    
    public Vector2 CameraPos = new(0, 0);
    public Vector2 CameraSize = new(Console.WindowWidth, Console.WindowHeight);
    public Vector2 Size = new(Console.WindowWidth, Console.WindowHeight);
    
    #endregion
    
    #region Private Fields
    
    private static readonly ConsoleColor DefaultBg = Console.BackgroundColor;
    private static readonly ConsoleColor DefaultFg = Console.ForegroundColor;
    private readonly ConsoleCanvas _canvas;
    private readonly ILog _log;
    
    #endregion
    
    #region Constructor
    
    public Renderer()
    {
        _canvas = new ConsoleCanvas().Render();
        _log = LogManager.GetLogger(GetType());
        Console.CursorVisible = false;
    }
    
    #endregion
    
    #region IEventSink Members
    
    public void ProcessEvent(IEvent evt)
    {
        throw new NotImplementedException();
    }
    
    #endregion
    
    #region IRenderer Members
    
    public void RenderComponents(in IStorage storage, double timeStepSizeMs, double howFarIntoNextFrameMs)
    {
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
    
    private bool IsInCamera(float x, float y)
    {
        return x >= CameraPos.X
               && y <= CameraSize.X - CameraPos.X
               && y >= CameraPos.Y
               && y <= CameraSize.Y - CameraPos.Y;
    }
    
    #endregion
}