using ConsoleRenderer;
using log4net;
using TermRTS.Event;
using TermRTS.Examples.Greenery.Ui;

namespace TermRTS.Examples.Greenery;

// TODO:
// - Extract Map and Textbox from the renderer into separate classes/structs
// - Remove OffsetX and OffsetY from map
// - Pack all ui elements into list
// - Rename RenderMode to MapRenderMode
public class Renderer : IRenderer, IEventSink
{
    #region Fields

    private static readonly ILog Log = LogManager.GetLogger(typeof(Renderer));
    private static readonly ConsoleColor DefaultBg = Console.BackgroundColor;
    private static readonly ConsoleColor DefaultFg = Console.ForegroundColor;
    private readonly ConsoleCanvas _canvas;

    // TODO: Find a more modular way of handling this.
    private readonly MapView _mapView;
    private readonly TextBox _textbox;

    private string _profileOutput;
    private double _timePassedMs;

    #endregion

    #region Constructor

    public Renderer(MapView mapView, TextBox textbox)
    {
        _canvas = new ConsoleCanvas().Render();
        _canvas.AutoResize = true;
        // _canvas.Interlaced = true;
        _mapView = mapView;
        _textbox = textbox;
        _profileOutput = string.Empty;

        Console.CursorVisible = false;
    }

    #endregion

    #region IEventSink Members

    public void ProcessEvent(IEvent evt)
    {
#if DEBUG
        if (evt is Event<Profile> (var profileContent)) _profileOutput = profileContent.ProfileInfo;
#endif

        if (!_textbox.IsOngoingInput && evt is Event<ConsoleKeyInfo> (var keyContent))
        {
            // TODO: Remove this if-query and create separate event input for mapview.
        }

        if (evt is Event<RenderMode> (var renderMode))
        {
            // TODO: Remove this if-query and create separate event input for mapview.
        }
    }

    #endregion

    #region IRenderer Members

    public void RenderComponents(
        in IStorage storage,
        double timeStepSizeMs,
        double howFarIntoNextFramePercent)
    {
        // Update viewport on Terminal resizing
        // TODO: Update mapviewSize as property of mapview
        if (Math.Abs(_canvas.Width - _mapView.Width) > 0.9
            || Math.Abs(_canvas.Height - _mapView.Height) > 0.9)
        {
            _mapView.Width = _canvas.Width;
            _mapView.Height = _canvas.Height;
        }

        // TODO: Step 1: Render map view

        // TODO: Step 2: Render text box
        // Render textbox if its contents have changed.
        if (_textbox.IsOngoingInput)
            RenderTextbox();
        else
            for (var i = 0; i < _canvas.Width; i += 1)
                _canvas.Set(i, _canvas.Height - 1, ' ', DefaultFg, DefaultBg);

        // Step 3: Render profiling info.
#if DEBUG
        if (!_textbox.IsOngoingInput)
            RenderDebugInfo(timeStepSizeMs, howFarIntoNextFramePercent);
#endif
    }

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

    // TODO: Move method to TextBox class.
    private void RenderTextbox()
    {
        // TODO: Cache x and y, update whenever viewport size changes
        var x = Convert.ToInt32(_mapView.Width);
        var y = Convert.ToInt32(_mapView.Height);
        var fg = DefaultFg;
        var bg = DefaultBg;

        // render blank line
        for (var i = 0; i < x; i += 1)
            _canvas.Set(i, y, ' ', bg, fg);

        // render prompt
        _canvas.Set(0, y, '>', bg, fg);
        _canvas.Set(1, y, ' ', bg, fg);

        // render text
        var input = _textbox.GetCurrentInput();
        for (var i = 0; i < input.Length; i += 1)
        {
            var c = input[i];
            _canvas.Set(2 + i, y, c, bg, fg);
        }
    }

    #endregion
}