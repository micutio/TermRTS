using ConsoleRenderer;
using log4net;
using System.Numerics;

namespace TermRTS.Examples.Greenery;

public class Renderer : IRenderer, IEventSink
{
    #region Public Fields

    public Vector2 CameraPos = new(0, 0);
    public Vector2 ViewportSize = new(Console.WindowWidth, Console.WindowHeight);
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
    }

    #endregion

    #region IRenderer Members

    public void RenderComponents(in IStorage storage, double timeStepSizeMs,
        double howFarIntoNextFrameMs)
    {
        var worldComponent = storage
            .GetForType(typeof(WorldComponent))
            .First();
        if (worldComponent is WorldComponent world) RenderWorld(world);
    }

    private void RenderWorld(WorldComponent world)
    {
        // TODO: Only update whenever CameraPos changes.
        var minX = (int)Math.Max(0, CameraPos.X - ViewportSize.X);
        var minY = (int)Math.Max(0, CameraPos.Y - ViewportSize.Y);
        var maxX = (int)(minX + ViewportSize.X);
        var maxY = (int)(minY + ViewportSize.Y);

        for (var y = minY; y < maxY; y++)
        for (var x = minX; x < maxX; x++)
        {
            var c = world.Cells[x, y] % 2 == 0 ? 'X' : '_';
            var colBg = DefaultBg;
            const ConsoleColor colFg = ConsoleColor.Green;
            _canvas.Set(x, y, c, colFg, colBg);
        }

        //throw new NotImplementedException();
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
               && y <= ViewportSize.X - CameraPos.X
               && y >= CameraPos.Y
               && y <= ViewportSize.Y - CameraPos.Y;
    }

    #endregion
}