using ConsoleRenderer;
using log4net;
using TermRTS.Event;
using TermRTS.Examples.Hillshade.Ui;
using TermRTS.Shared.Ui;
using TermRTS.Storage;
using TermRTS.Ui;

namespace TermRTS.Examples.Hillshade;

public class Renderer : UiRootBase, IRenderer, IEventSink
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(Renderer));
    private static readonly ConsoleColor DefaultBg = Console.BackgroundColor;
    private static readonly ConsoleColor DefaultFg = Console.ForegroundColor;

    private readonly ConsoleCanvas _canvas;
    private readonly HillshadeMapView _mapview;
    private readonly SchedulerEventQueue _evtQueue;
    private ulong _currentTimeStepSizeMs = 16;
    private int _lastCanvasWidth;
    private int _lastCanvasHeight;
    private ulong _timeOfDayMs;
    private ulong _dayLengthMs = 1;

    public Renderer(SchedulerEventQueue evtQueue, int worldWidth, int worldHeight)
    {
        _evtQueue = evtQueue;
        _canvas = ConsoleCanvasSetup.CreateRenderedCanvas();
        _lastCanvasWidth = _canvas.Width;
        _lastCanvasHeight = _canvas.Height;
        _mapview = new HillshadeMapView(_canvas, worldWidth, worldHeight)
        {
            Height = _canvas.Height - 1,
            Width = _canvas.Width
        };
        AddUiElement(_mapview);
    }

    public void ProcessEvent(IEvent evt)
    {
        if (evt is not Event<ConsoleKeyInfo>(var keyInfo))
            return;

        if (keyInfo.Key == ConsoleKey.OemPlus || keyInfo.Key == ConsoleKey.Add)
        {
            _currentTimeStepSizeMs = Math.Min(1000UL * 60 * 60 * 24, _currentTimeStepSizeMs * 2);
            _evtQueue.EnqueueEvent(ScheduledEvent.From(new TimeScaleChanged(_currentTimeStepSizeMs)));
            return;
        }

        if (keyInfo.Key == ConsoleKey.OemMinus || keyInfo.Key == ConsoleKey.Subtract)
        {
            _currentTimeStepSizeMs = Math.Max(1UL, _currentTimeStepSizeMs / 2);
            _evtQueue.EnqueueEvent(ScheduledEvent.From(new TimeScaleChanged(_currentTimeStepSizeMs)));
            return;
        }

        _mapview.HandleKeyInput(in keyInfo);
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

    public void RenderComponents(
        in IReadonlyStorage storage,
        double timeStepSizeMs,
        double howFarIntoNextFramePercent)
    {
        CheckForCanvasSizeChanged();
        UpdateFromComponents(storage, timeStepSizeMs, howFarIntoNextFramePercent);
        Render();
        RenderStatusLine();
    }

    protected override void UpdateThisFromComponents(
        in IReadonlyStorage componentStorage,
        double timeStepSizeMs,
        double howFarIntoNextFramePercent)
    {
        if (componentStorage.TryGetSingleForType<TimeOfDayComponent>(out var timeOfDay) && timeOfDay != null)
        {
            _timeOfDayMs = timeOfDay.TimeMs;
            _dayLengthMs = timeOfDay.DayLengthMs > 0 ? timeOfDay.DayLengthMs : 1;
        }
    }

    protected override void RenderUiBase()
    {
        for (var i = 0; i < _canvas.Width; i++)
            _canvas.Set(i, _canvas.Height - 1, ' ', DefaultFg, DefaultBg);
    }

    protected override void OnXChanged()
    {
        _mapview.X = X;
    }

    protected override void OnYChanged()
    {
        _mapview.Y = Y;
    }

    protected override void OnWidthChanged()
    {
        _mapview.Width = Width;
    }

    protected override void OnHeightChanged()
    {
        _mapview.Height = Height - 1;
    }

    private void CheckForCanvasSizeChanged()
    {
        if (Math.Abs(_canvas.Width - _lastCanvasWidth) < 0.9 && Math.Abs(_canvas.Height - _lastCanvasHeight) < 0.9)
            return;
        _lastCanvasWidth = _canvas.Width;
        _lastCanvasHeight = _canvas.Height;
        _mapview.Width = _canvas.Width;
        _mapview.Height = _canvas.Height - 1;
        IsRequireReRender = true;
        IsRequireRootReRender = true;
    }

    private void RenderStatusLine()
    {
        var phase = _dayLengthMs > 0 ? (double)(_timeOfDayMs % _dayLengthMs) / _dayLengthMs : 0;
        var dayHour = phase * 24.0;
        var h = (int)dayHour % 24;
        var m = (int)((dayHour - Math.Floor(dayHour)) * 60);
        var timeOfDayStr = $"{h:D2}:{m:D2}";
        var line = $"{timeOfDayStr} | + / - time step ({_currentTimeStepSizeMs} ms) | Arrows: pan";
        if (line.Length > _canvas.Width) line = line[.._canvas.Width];
        _canvas.Text(0, _canvas.Height - 1, line, false, DefaultBg, DefaultFg);
    }
}
