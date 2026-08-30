using ConsoleRenderer;
using Microsoft.Extensions.Logging;
using TermRTS.Event;
using TermRTS.Examples.Greenery.Ui;
using TermRTS.Log;
using TermRTS.Storage;
using TermRTS.Ui;

namespace TermRTS.Examples.Greenery;

public class Renderer : UiElementBase, IRenderer, IEventSink
{
    #region Fields

    private static ILogger<Renderer> Log => TermRtsLog.For<Renderer>();
    private const ConsoleColor DefaultBg = ConsoleColor.Black;
    private const ConsoleColor DefaultFg = ConsoleColor.Gray;

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

    public Renderer(SchedulerEventQueue evtQueue, int worldWidth, int worldHeight, UiThemes theme)
    {
        _canvas = new ConsoleCanvas().Render();
        _canvas.AutoResize = true;
        _lastCanvasWidth = _canvas.Width;
        _lastCanvasHeight = _canvas.Height;

        _mapview = new MapView(worldWidth, worldHeight, theme)
        {
            Height = _canvas.Height,
            Width = (int)(_canvas.Width * 0.7f)
        };
        _logArea = new LogArea(_canvas.Height - 1)
        {
            X = _mapview.Width + 1,
            Y = 1,
            Width = _canvas.Width - _mapview.Width,
            Height = _canvas.Height - 1
        };
        _textbox = new TextBox(evtQueue)
        {
            X = _mapview.Width + 1,
            Y = 0,
            Width = _canvas.Width - _mapview.Width,
            Height = 1
        };
        AddChildUiElement(_mapview);
        AddChildUiElement(_logArea);
        AddChildUiElement(_textbox);
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
        if (evt is Event<Profile>(var profileContent)) _profileOutput = profileContent.ProfileInfo;
#endif

        // TODO: Implement handling of focus requests
        if (evt is Event<ConsoleKeyInfo>(var keyInfo))
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
        Log.LogInformation("Shutting down renderer.");
    }

    public void RenderComponents(
        in IReadonlyStorage storage,
        double timeStepSizeMs,
        double howFarIntoNextFramePercent)
    {
        CheckForCanvasSizeChanged();
        var ctx = new RenderContext(
            new CanvasAdapter(_canvas),
            new Rect(0, 0, _canvas.Width, _canvas.Height));
        UpdateUiTreeFromComponents(storage, timeStepSizeMs, howFarIntoNextFramePercent);
        RenderUiTree(ctx);
#if DEBUG
        RenderDebugInfo(_timeStepSizeMs, _howFarIntoNextFramePercent);
#endif
    }

    #endregion

    #region UiElementBase Members

    public override void UpdateSelfFromComponents(
        in IReadonlyStorage componentStorage,
        double timeStepSizeMs,
        double howFarIntoNextFramePercent)
    {
        _timeStepSizeMs = timeStepSizeMs;
        _howFarIntoNextFramePercent = howFarIntoNextFramePercent;
    }

    protected override void RenderSelf(RenderContext ctx)
    {
    }

    #endregion

    #region UiElementBase Members

    protected override void OnXChanged()
    {
        _mapview.X = X;
        _logArea.X = X + _mapview.Width;
        _textbox.X = X;
    }

    protected override void OnYChanged()
    {
        _mapview.Y = Y;
        _logArea.Y = Y;
        _textbox.Y = Y + _mapview.Height - 1;
    }

    protected override void OnWidthChanged()
    {
        _mapview.Width = (int)(Width * 0.7);
        _logArea.Width = Width - _mapview.Width;
        _textbox.Width = Width;
    }

    protected override void OnHeightChanged()
    {
        _mapview.Height = Height - 1;
        _logArea.Height = Height - 1;
        // _textbox.Height remains constant at 1
    }

    #endregion

    #region Private Members

    private void CheckForCanvasSizeChanged()
    {
        // Update viewport on Terminal resizing
        if (!(Math.Abs(_canvas.Width - _lastCanvasWidth) > 0.9)
            && !(Math.Abs(_canvas.Height - _lastCanvasHeight) > 0.9)) return;
        _lastCanvasWidth = _canvas.Width;
        _lastCanvasHeight = _canvas.Height;
        _mapview.Width = (int)(_canvas.Width * 0.7);
        _mapview.Height = _canvas.Height - 1;
        _logArea.X = _mapview.Width + 1;
        _logArea.Width = _canvas.Width - _mapview.Width;
        _logArea.Height = _canvas.Height - 1;
        _textbox.Y = _mapview.Height - 1;
        _textbox.Width = _mapview.Width;

        IsRequireRender = true;
        IsRequireRootRender = true;
    }

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