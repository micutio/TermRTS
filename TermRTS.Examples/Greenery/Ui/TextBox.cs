using System.Threading.Channels;
using ConsoleRenderer;
using TermRTS.Event;
using TermRTS.Ui;

namespace TermRTS.Examples.Greenery.Ui;

internal enum InputState
{
    Idle,
    OngoingInput
}

public class TextBox(ConsoleCanvas canvas) : KeyInputProcessorBase
{
    #region Fields

    private readonly ConsoleColor DefaultBg = Console.BackgroundColor;
    private readonly ConsoleColor DefaultFg = Console.ForegroundColor;

    private readonly Channel<ScheduledEvent> _channel = Channel.CreateUnbounded<ScheduledEvent>();
    private readonly char[] _msg = new char[80];
    private int _idx;
    private InputState _state = InputState.Idle;

    #endregion

    #region Properties

    public ChannelReader<ScheduledEvent> MessageEventReader => _channel.Reader;

    public bool IsOngoingInput => _state == InputState.OngoingInput;

    #endregion

    #region IEventSink Members

    public override void HandleKeyInput(in ConsoleKeyInfo keyInfo)
    {
        if (keyInfo.Key == ConsoleKey.Enter)
            switch (_state)
            {
                case InputState.Idle:
                    _state = InputState.OngoingInput;
                    IsRequireReRender = true;
                    Array.Clear(_msg, 0, 80);
                    return;
                case InputState.OngoingInput:
                    FinalizeMessage();
                    _state = InputState.Idle;
                    return;
                default:
                    return;
            }

        if (!IsOngoingInput) return;

        switch (keyInfo.Key)
        {
            case ConsoleKey.Spacebar:
                _msg[_idx] = ' ';
                _idx += 1;
                break;
            case ConsoleKey.Backspace:
                _idx = Math.Max(_idx - 1, 0);
                break;
            default:
                _msg[_idx] = keyInfo.KeyChar;
                _idx += 1;
                break;
        }
    }

    #endregion

    #region UiElementBase Members

    public override void UpdateFromComponents(
        in IStorage componentStorage,
        double timeStepSizeMs,
        double howFarIntoNextFramePercent)
    {
        // Does not require components to work.
    }

    public override void Render()
    {
        if (!IsOngoingInput) return;

        var fg = DefaultFg;
        var bg = DefaultBg;

        // TODO: How to decouple from canvas class implementations?
        // render blank line
        for (var i = X; i < Width; i += 1)
            canvas.Set(i, Y, ' ', bg, fg);

        // render prompt
        canvas.Set(X, Y, '>', bg, fg);
        canvas.Set(X + 1, Y, ' ', bg, fg);

        // render text
        var input = GetCurrentInput();
        var startX = X + 2;
        for (var i = 0; i < input.Length; i += 1)
        {
            var c = input[i];
            canvas.Set(startX + i, Y, c, bg, fg);
        }
    }

    protected override void OnXChanged(int newX)
    {
        IsRequireReRender = true;
    }

    protected override void OnYChanged(int newY)
    {
        IsRequireReRender = true;
    }

    protected override void OnWidthChanged(int newWidth)
    {
        IsRequireReRender = true;
    }

    protected override void OnHeightChanged(int newHeight)
    {
        IsRequireReRender = true;
    }

    #endregion

    #region Members

    private void FinalizeMessage()
    {
        _idx = 0;
        _state = InputState.Idle;
        _channel.Writer.TryWrite(ScheduledEvent.From(new Event.Command(_msg)));
    }

    private ReadOnlySpan<char> GetCurrentInput()
    {
        return _idx == 0
            ? new ReadOnlySpan<char>(_msg, 0, 0)
            : new ReadOnlySpan<char>(_msg, 0, _idx);
    }

    #endregion
}