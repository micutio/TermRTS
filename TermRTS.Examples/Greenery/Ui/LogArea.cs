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

    private const int PaddingTop = 1;
    private const int PaddingLeft = 1;
    private const int PaddingRight = 1;
    private const int PaddingBottom = 1;

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
        var logEntryLine = "" + Cp437.Greater + Cp437.WhiteSpace;
        const int promptSpace = 2;
        var currentLineWidth = promptSpace; // prompt size
        var wordArray = message.Split([' '], StringSplitOptions.RemoveEmptyEntries);
        var words = new RingBuffer<string>(wordArray.Length, wordArray);
        while (words.Size > 0)
        {
            var word = words.Front();
            words.PopFront();
            // word is too long for the entire line
            if (word.Length > lineWidth + promptSpace)
            {
                // split the word
                var head = word[..(lineWidth + promptSpace)];
                // put the rest back on the buffer
                var tail = word[(lineWidth + promptSpace)..];
                words.PushFront(tail);
                // continue with the front
                word = head;
            }

            // word does not fit into the current line anymore either
            if (word.Length > lineWidth - currentLineWidth)
            {
                var head = word[..(lineWidth - currentLineWidth)];
                var tail = word[(lineWidth - currentLineWidth)..];
                words.PushFront(tail);
                word = head;
                // finish this line and create a new one
                _buffer.PushBack(logEntryLine);
                logEntryLine = word + Cp437.WhiteSpace;
                currentLineWidth = word.Length + 1;
            }
            else
            {
                logEntryLine += word + Cp437.WhiteSpace;
                currentLineWidth += word.Length + 1;
            }
        }

        _buffer.PushBack(logEntryLine);
    }


    public void UpdateLayout(int newWidth, int newHeight)
    {
        var messages = _buffer.ToArray();
        _buffer = new RingBuffer<string>(newHeight);
        var paddedLineWidth = newWidth + PaddingLeft + PaddingRight;
        foreach (var msg in messages) AddLogEntry(paddedLineWidth, msg);
    }

    #endregion

    #region IEventSink Members

    public void ProcessEvent(IEvent evt)
    {
        if (evt is not Event<SystemLog>(var logContent)) return;
        var paddedLineWidth = Width + PaddingLeft + PaddingRight;
        AddLogEntry(paddedLineWidth, logContent.Content);
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
        var idx = 0;
        // alternate background color with every new log entry
        var altBackground = true;
        foreach (var msg in _buffer)
        {
            if (msg[0].Equals(Cp437.Greater)) altBackground = !altBackground;
            var bgColor = altBackground ? ConsoleColor.Black : ConsoleColor.DarkGray;
            canvas.Text(X, Y + idx, msg, false, DefaultFg, bgColor);

            // fill the rest of the line with the same background color
            for (var i = msg.Length; i < Width; i++)
                canvas.Set(X + i, Y + idx, Cp437.WhiteSpace, DefaultFg, bgColor);
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