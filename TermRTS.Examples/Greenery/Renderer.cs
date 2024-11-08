using ConsoleRenderer;
using log4net;
using System.Numerics;

namespace TermRTS.Examples.Greenery;

public class Renderer : IRenderer, IEventSink
{
    #region Public Fields

    private readonly Vector2 _viewportSize;
    private readonly Vector2 _worldSize;

    private Vector2 _cameraPos = new(0, 0);

    #endregion

    #region Private Fields

    private static readonly ConsoleColor DefaultBg = Console.BackgroundColor;
    private static readonly ConsoleColor DefaultFg = Console.ForegroundColor;
    private readonly ConsoleCanvas _canvas;
    private readonly ILog _log;

    // TODO: Find a more modular way of handling this.
    private readonly TextBox _textbox;

    #endregion

    #region Constructor

    public Renderer(int viewportWidth, int viewportHeight, int worldWidth, int worldHeight)
    {
        _canvas = new ConsoleCanvas().Render();
        _log = LogManager.GetLogger(GetType());
        _textbox = new TextBox();
        _viewportSize.X = viewportWidth;
        _viewportSize.Y = viewportHeight;
        _worldSize.X = worldWidth;
        _worldSize.Y = worldHeight;
        Console.CursorVisible = false;
    }

    #endregion

    #region IEventSink Members

    public void ProcessEvent(IEvent evt)
    {
        if (evt.Type() != EventType.KeyInput) return;

        var keyEvent = (KeyInputEvent)evt;
        switch (keyEvent.Info.Key)
        {
            case ConsoleKey.UpArrow:
                MoveCameraUp();
                return;
            case ConsoleKey.DownArrow:
                MoveCameraDown();
                return;
            case ConsoleKey.LeftArrow:
                MoveCameraLeft();
                return;
            case ConsoleKey.RightArrow:
                MoveCameraRight();
                return;
        }

        _textbox.ProcessEvent(evt);
    }

    #endregion

    #region IRenderer Members

    public void RenderComponents(
        in IStorage storage,
        double timeStepSizeMs,
        double howFarIntoNextFrameMs)
    {
        var worldComponent = storage
            .GetForType(typeof(WorldComponent))
            .First();
        if (worldComponent is WorldComponent world) RenderWorld(world);

        if (!_textbox.IsOngoingInput) return;

        var x = Convert.ToInt32(_viewportSize.X - 1);
        var y = Convert.ToInt32(_viewportSize.Y - 1);
        var fg = DefaultFg;
        var bg = DefaultBg;

        for (var i = 0; i < x; i += 1)
            _canvas.Set(i, y, ' ', bg, fg);

        _canvas.Set(0, y, '>', bg, fg);
        _canvas.Set(1, y, ' ', bg, fg);

        var input = _textbox.GetCurrentInput();
        for (var i = 0; i < input.Count; i += 1)
        {
            var c = input[i];
            _canvas.Set(2 + i, y, c, bg, fg);
        }
    }

    private void RenderWorld(WorldComponent world)
    {
        // TODO: Only update whenever CameraPos changes.
        var minX = Convert.ToInt32(_cameraPos.X);
        var minY = Convert.ToInt32(_cameraPos.Y);
        var maxX = Convert.ToInt32(Math.Min(_cameraPos.X + _viewportSize.X, _worldSize.X));
        var maxY = Convert.ToInt32(Math.Min(_cameraPos.Y + _viewportSize.Y, _worldSize.Y));

        for (var y = minY; y < maxY; y++)
        for (var x = minX; x < maxX; x++)
        {
            // var c = world.Cells[x, y] % 2 == 0 ? 'X' : '_';
            var c = Convert.ToString(world.Cells[x, y])[0];
            var colBg = DefaultBg;
            // const ConsoleColor colFg = ConsoleColor.Green;
            var colFg = c switch
            {
                '0' => ConsoleColor.DarkBlue,
                '1' => ConsoleColor.Blue,
                '2' => ConsoleColor.DarkCyan,
                '3' => ConsoleColor.Cyan,
                '4' => ConsoleColor.Yellow,
                '5' => ConsoleColor.DarkGreen,
                '6' => ConsoleColor.Green,
                '7' => ConsoleColor.DarkYellow,
                '8' => ConsoleColor.DarkGray,
                _ => ConsoleColor.Gray
            };

            _canvas.Set(x - minX, y - minY, c, colFg, colBg);
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

    private void MoveCameraUp()
    {
        _cameraPos.Y = Math.Max(_cameraPos.Y - 1, 0);
    }

    private void MoveCameraDown()
    {
        _cameraPos.Y = Math.Max(0, Math.Min(_cameraPos.Y + 1, _worldSize.Y - _viewportSize.Y));
    }

    private void MoveCameraLeft()
    {
        _cameraPos.X = Math.Max(_cameraPos.X - 1, 0);
    }

    private void MoveCameraRight()
    {
        _cameraPos.X = Math.Max(0, Math.Min(_cameraPos.X + 1, _worldSize.X - _viewportSize.X));
    }

    private bool IsInCamera(float x, float y)
    {
        return x >= _cameraPos.X
               && y <= _cameraPos.X + _viewportSize.X
               && y >= _cameraPos.Y
               && y <= _cameraPos.Y + _viewportSize.Y;
    }

    #endregion
}