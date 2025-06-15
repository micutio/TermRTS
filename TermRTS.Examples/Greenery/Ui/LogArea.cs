using ConsoleRenderer;
using TermRTS.Data;
using TermRTS.Event;
using TermRTS.Io;
using TermRTS.Ui;

namespace TermRTS.Examples.Greenery.Ui;

// TODO: Implementation Ideas
//       - Ringbuffer, containing lines of text
//       - Method for adding new text into buffer
//       - make it scrollable?
public class LogArea : UiElementBase, IEventSink
{
    #region Fields

    private static readonly ConsoleColor DefaultBg = Console.BackgroundColor;
    private static readonly ConsoleColor DefaultFg = Console.ForegroundColor;

    private readonly ConsoleCanvas _canvas;

    private RingBuffer<string> _buffer;

    #endregion

    #region Constructor

    public LogArea(ConsoleCanvas canvas, int capacity)
    {
        _canvas = canvas;
        _buffer = new RingBuffer<string>(capacity);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds a log entry to the log. Depending on length, it may be distributed over several lines.
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    public void AddLogEntry(int lineWidth, string message)
    {
        // TODO: Create algorithm to distribute the string into the current layout.
        var fullLineWidth = Width;
        var logEntryLine = "";
        var currentLineWidth = 0;
        var wordArray = message.Split([' '], StringSplitOptions.RemoveEmptyEntries);
        var words = new RingBuffer<string>(wordArray.Length, wordArray);
        while (words.Size > 0)
        {
            var word = words.Front();
            words.PopFront();
            if (word.Length > fullLineWidth)
            {
                // split the word
                var head = word.Substring(0, fullLineWidth);
                // put the rest back on the buffer
                var tail = word.Substring(fullLineWidth);
                words.PushFront(tail);
                // continue with the front
                word = head;
            }

            if (word.Length > fullLineWidth - currentLineWidth)
            {
                var head = word.Substring(0, fullLineWidth - currentLineWidth);
                var tail = word.Substring(fullLineWidth - currentLineWidth);
                words.PushFront(tail);
                word = head;
            }

            logEntryLine += word + Cp437.WhiteSpace;
            currentLineWidth += word.Length + 1;
            _buffer.PushBack(logEntryLine);
        }
    }


    public void UpdateLayout(int newWidth, int newHeight)
    {
        var messages = _buffer.ToArray();
        _buffer = new RingBuffer<string>(newHeight);
        foreach (var msg in messages) AddLogEntry(newWidth, msg);
    }

    #endregion

    #region IEventSink Members

    public void ProcessEvent(IEvent evt)
    {
        if (evt is not Event<SystemLog>(var logContent)) return;
        AddLogEntry(Width, logContent.Content);
        IsRequireReRender = true;
    }

    #endregion

    #region UiElementBase Members

    public override void UpdateFromComponents(
        in IReadonlyStorage componentStorage,
        double timeStepSizeMs,
        double howFarIntoNextFramePercent)
    {
        // Does not require components to work.
    }

    public override void Render()
    {
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
            _canvas.Set(x, y, Cp437.BlockFull, DefaultFg, DefaultBg);

        var idx = 0;
        foreach (var msg in _buffer)
        {
            _canvas.Text(X, Y + idx, msg);
            idx++;
        }
    }

    protected override void OnXChanged(int newX)
    {
        IsRequireReRender = true;
        IsRequireRootReRender = true;
    }

    protected override void OnYChanged(int newY)
    {
        IsRequireReRender = true;
        IsRequireRootReRender = true;
    }

    protected override void OnWidthChanged(int newWidth)
    {
        UpdateLayout(newWidth, Height);
        IsRequireReRender = true;
        IsRequireRootReRender = true;
    }

    protected override void OnHeightChanged(int newHeight)
    {
        UpdateLayout(Width, newHeight);
        IsRequireReRender = true;
        IsRequireRootReRender = true;
    }

    #endregion
}