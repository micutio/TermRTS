using ConsoleRenderer;
using log4net;
using TermRTS.Event;
using TermRTS.Examples.Greenery.Ui;
using TermRTS.Ui;

namespace TermRTS.Examples.Greenery;

// TODO:
// - Implement handling of focus requests
public class Renderer : UiRootBase, IRenderer, IEventSink
{
    #region Fields

    private static readonly ILog Log = LogManager.GetLogger(typeof(Renderer));
    private static readonly ConsoleColor DefaultBg = Console.BackgroundColor;
    private static readonly ConsoleColor DefaultFg = Console.ForegroundColor;

    private readonly ConsoleCanvas _canvas;
    private readonly MapView _mapview;
    private readonly LogArea _logArea;
    private readonly TextBox _textbox;

    private int _lastCanvasWidth;
    private int _lastCanvasHeight;
    private string _profileOutput;
    private double _timePassedMs;
    private double _timeStepSizeMs;
    private double _howFarIntoNextFramePercent;

    #endregion

    #region Constructor

    public Renderer(int worldWidth, int worldHeight)
    {
        _canvas = new ConsoleCanvas().Render();
        _canvas.AutoResize = true;
        _lastCanvasWidth = _canvas.Width;
        _lastCanvasHeight = _canvas.Height;
        // _canvas.Interlaced = true;
        _mapview = new MapView(_canvas, worldWidth, worldHeight)
        {
            Height = _canvas.Height - 1,
            Width = (int)(_canvas.Width * 0.7)
        };
        _logArea = new LogArea(_canvas, _canvas.Height - 1)
        {
            X = _mapview.Width + 1,
            Height = _canvas.Height - 1,
            Width = _canvas.Width - _mapview.Width
        };
        _textbox = new TextBox(_canvas)
        {
            Y = _mapview.Height,
            Width = _canvas.Width
        };
        AddUiElement(_mapview);
        AddUiElement(_logArea);
        AddUiElement(_textbox);
        _profileOutput = string.Empty;

        Console.CursorVisible = false;
    }

    #endregion

    #region Properties

    public LogArea LogArea => _logArea;
    public TextBox Textbox => _textbox;

    #endregion

    #region IEventSink Members

    public void ProcessEvent(IEvent evt)
    {
#if DEBUG
        if (evt is Event<Profile> (var profileContent)) _profileOutput = profileContent.ProfileInfo;
#endif

        // TODO: Implement handling of focus requests
        if (evt is Event<ConsoleKeyInfo> (var keyInfo))
        {
            _textbox.HandleKeyInput(in keyInfo);
            if (!_textbox.IsOngoingInput) _mapview.HandleKeyInput(in keyInfo);
        }

        // TODO: Remove this if-query and create separate event input for mapview.
        if (evt is Event<MapRenderMode>) _mapview.ProcessEvent(evt);
    }

    #endregion

    #region IRenderer Members

    public void FinalizeRender()
    {
        _canvas.Render();
    }

    public void Shutdown()
    {
        Console.ResetColor();
        Console.Clear();
        Log.Info("Shutting down renderer.");
    }

    public void RenderComponents(
        in IReadonlyStorage storage,
        double timeStepSizeMs,
        double howFarIntoNextFramePercent)
    {
        // This calls UiElementBase.UpdateFromComponents,
        // which calls UiRootBase.UpdateThisFromComponents
        UpdateFromComponents(storage, timeStepSizeMs, howFarIntoNextFramePercent);
        // This calls UiElementBase.Render(), which calls UiRootBase.RenderUiBase().
        Render();
#if DEBUG
        if (!_textbox.IsOngoingInput)
            RenderDebugInfo(_timeStepSizeMs, _howFarIntoNextFramePercent);
#endif
    }

    #endregion

    #region UiRootBase Members

    protected override void UpdateThisFromComponents(
        in IReadonlyStorage componentStorage,
        double timeStepSizeMs,
        double howFarIntoNextFramePercent)
    {
        // TODO: Implement layouting of child elements
        _timeStepSizeMs = timeStepSizeMs;
        _howFarIntoNextFramePercent = howFarIntoNextFramePercent;
    }

    protected override void RenderUiBase()
    {
        // Update viewport on Terminal resizing
        if (Math.Abs(_canvas.Width - _lastCanvasWidth) > 0.9
            || Math.Abs(_canvas.Height - _lastCanvasHeight) > 0.9)
        {
            _lastCanvasWidth = _canvas.Width;
            _lastCanvasHeight = _canvas.Height;
            _mapview.Width = (int)(_canvas.Width * 0.7);
            _mapview.Height = _canvas.Height - 1;
            _logArea.X = _mapview.Width + 1;
            _logArea.Width = _canvas.Width - _mapview.Width;
            _logArea.Height = _canvas.Height - 1;
            _textbox.Y = _canvas.Height - 1;
            _textbox.Width = _mapview.Width;
        }

        if (_textbox.IsOngoingInput) return;
        for (var i = 0; i < _canvas.Width; i += 1)
            _canvas.Set(i, _canvas.Height - 1, ' ', DefaultFg, DefaultBg);
    }

    #endregion

    #region UiElementBase Members

    protected override void OnXChanged(int newX)
    {
        // TODO: Implement proper layouting
        _mapview.X = newX;
        _logArea.X = newX + _mapview.Width;
        _textbox.X = newX;
    }

    protected override void OnYChanged(int newY)
    {
        // TODO: Implement proper layouting
        _mapview.Y = newY;
        _logArea.Y = newY;
        _textbox.Y = newY + _mapview.Height;
    }

    protected override void OnWidthChanged(int newWidth)
    {
        // TODO: Implement proper layouting
        _mapview.Width = (int)(newWidth * 0.7);
        _logArea.Width = newWidth - _mapview.Width;
        _textbox.Width = newWidth;
    }

    protected override void OnHeightChanged(int newHeight)
    {
        // TODO: Implement proper layouting
        _mapview.Height = newHeight - 1;
        _logArea.Height = newHeight - 1;
        // _textbox.Height remains constant at 1
    }

    #endregion

    #region Private Members

    private void RenderDebugInfo(double timeStepSizeMs, double howFarIntoNextFramePercent)
    {
        _timePassedMs += timeStepSizeMs + timeStepSizeMs * howFarIntoNextFramePercent;

        var debugStr = string.IsNullOrEmpty(_profileOutput)
            ? string.Empty
            : _profileOutput;
        var sec = (int)Math.Floor(_timePassedMs / 1000) % 60;
        var min = (int)Math.Floor(_timePassedMs / (1000 * 60)) % 60;
        var hr = (int)Math.Floor(_timePassedMs / (1000 * 60 * 60)) % 24;
        _canvas.Text(0, _canvas.Height - 1, $"{hr:D2}:{min:D2}:{sec:D2} | {debugStr}");
    }

    #endregion
}