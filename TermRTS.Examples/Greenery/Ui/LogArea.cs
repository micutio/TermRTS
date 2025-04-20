using ConsoleRenderer;
using TermRTS.Event;

namespace TermRTS.Examples.Greenery.Ui;

public class LogArea : IEventSink
{
    #region Fields

    private readonly Queue<string> _logEntries = [];

    #endregion

    #region Properties

    private int MinX { get; set; }
    private int MinY { get; set; }
    private int MaxX { get; set; }
    private int MaxY { get; set; }

    #endregion

    #region IEventSink Members

    public void ProcessEvent(IEvent evt)
    {
        if (evt is not Event<SystemLog>(var logContent)) return;

        _logEntries.Enqueue(logContent.Content);
    }

    #endregion

    #region Public Members

    public void Render(ref ConsoleCanvas canvas)
    {
        // TODO: Finalize idea
        // 
    }

    #endregion
}