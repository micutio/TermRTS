using System.Numerics;
using ConsoleRenderer;
using log4net;
using TermRTS.Event;
using TermRTS.Shared.Ui;
using TermRTS.Storage;

namespace TermRTS.Examples.Circuitry;

internal class Renderer : IRenderer, IEventSink
{
    #region Fields

    private static readonly ConsoleColor DefaultBg = Console.BackgroundColor;
    private static readonly ConsoleColor DefaultFg = Console.ForegroundColor;
    private readonly ConsoleCanvas _canvas;
    private readonly ILog _log;
    private string _profileOutput;
    private double _timePassedMs;

    public Vector2 CameraPos = new(0, 0);
    public Vector2 CameraSize = new(Console.WindowWidth, Console.WindowHeight);
    public Vector2 Size = new(Console.WindowWidth, Console.WindowHeight);

    #endregion

    #region Constructor

    public Renderer()
    {
        _canvas = ConsoleCanvasSetup.CreateRenderedCanvas();
        _log = LogManager.GetLogger(GetType());
        _profileOutput = string.Empty;
    }

    #endregion

    #region IEventSink Members

    /// <inheritdoc />
    public void ProcessEvent(IEvent evt)
    {
        if (evt is not Event<Profile>(var profile)) return;
        _profileOutput = profile.ProfileInfo;
    }

    #endregion

    #region IRenderer Members

    public void RenderComponents(in IReadonlyStorage storage, double timeStepSizeMs,
        double howFarIntoNextFramePercent)
    {
        RenderInfo(timeStepSizeMs, howFarIntoNextFramePercent);

        foreach (var chipOutline in storage.GetAllForType<Circuitry.Chip>()
                     .Select(chip => chip.Outline))
            RenderOutline(chipOutline);

        foreach (var bus in storage.GetAllForType<Circuitry.Bus>())
        {
            var progress = bus.IsForward ? bus.Progress : 1.0f - bus.Progress;
            foreach (var wire in bus.Connections) RenderWire(wire.Outline, bus.IsActive, progress);
        }
    }

    public void FinalizeRender()
    {
        _canvas.Render();
    }

    public void Shutdown()
    {
        Console.ResetColor();
    }

    #endregion

    #region Private Methods

    private void RenderInfo(double timeStepSizeMs, double howFarIntoNextFrameMs)
    {
        _timePassedMs += timeStepSizeMs + howFarIntoNextFrameMs;

        //#if DEBUG
        var line = StatusLineText.FormatWithAppLabel("Circuitry", _timePassedMs, _profileOutput);
        _canvas.Text(1, 0, line);
        //#endif
    }

    private void RenderOutline(IReadOnlyList<Circuitry.Cell> outline)
    {
        for (var i = 0; i < outline.Count; i += 1)
        {
            var (x, y, c) = outline[i];
            if (!IsInCamera(x, y)) continue;
            _canvas.Set(
                Convert.ToInt32(x - CameraPos.X),
                Convert.ToInt32(y - CameraPos.Y),
                c,
                ConsoleColor.Black);
        }
    }

    private void RenderWire(IReadOnlyList<Circuitry.Cell> outline, bool isActive, float progress)
    {
        var sparkIdx = Convert.ToInt32(outline.Count * progress);
        for (var i = 0; i < outline.Count; i++)
        {
            var (x, y, c) = outline[i];
            var deltaX = Convert.ToInt32(x - CameraPos.X);
            var deltaY = Convert.ToInt32(y - CameraPos.Y);

            if (isActive && i == sparkIdx)
                _canvas.Set(deltaX, deltaY, c, ConsoleColor.Blue, DefaultBg);
            else
                _canvas.Set(deltaX, deltaY, c, ConsoleColor.Black, DefaultBg);
        }
    }

    private bool IsInCamera(float x, float y)
    {
        return x >= CameraPos.X
               && y <= CameraSize.X - CameraPos.X
               && y >= CameraPos.Y
               && y <= CameraSize.Y - CameraPos.Y;
    }

    #endregion
}