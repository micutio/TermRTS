using ConsoleRenderer;

namespace TermRTS.Shared.Ui;

public static class ConsoleCanvasSetup
{
    /// <summary>
    ///     Creates a <see cref="ConsoleCanvas" />, calls <see cref="ConsoleCanvas.Render" />, and hides the cursor.
    /// </summary>
    public static ConsoleCanvas CreateRenderedCanvas(bool autoResize = true)
    {
        var canvas = new ConsoleCanvas().Render();
        canvas.AutoResize = autoResize;
        Console.CursorVisible = false;
        return canvas;
    }
}
