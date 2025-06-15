using ConsoleRenderer;
using TermRTS.Data;
using TermRTS.Event;
using TermRTS.Io;
using TermRTS.Ui;

namespace TermRTS.Examples.Greenery.Ui;

// TODO: Implementation Ideas
//       - RingBuffer, containing lines of text
//       - Method for adding new text into buffer
//       - make it scrollable?
public class LogArea(ConsoleCanvas canvas, int capacity) : UiElementBase, IEventSink
{
    #region Fields

    private static readonly ConsoleColor DefaultBg = Console.BackgroundColor;
    private static readonly ConsoleColor DefaultFg = Console.ForegroundColor;

    private RingBuffer<string> _buffer = new(capacity);

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
                var head = word[..fullLineWidth];
                // put the rest back on the buffer
                var tail = word[fullLineWidth..];
                words.PushFront(tail);
                // continue with the front
                word = head;
            }

            if (word.Length > fullLineWidth - currentLineWidth)
            {
                var head = word[..(fullLineWidth - currentLineWidth)];
                var tail = word[(fullLineWidth - currentLineWidth)..];
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
        for (var y = Y; y < Height; y++)
        for (var x = X; x < Width; x++)
            canvas.Set(x, y, Cp437.BlockFull, DefaultBg, DefaultBg);

        var idx = 0;
        foreach (var msg in _buffer)
        {
            canvas.Text(X, Y + idx, msg);
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