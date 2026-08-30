using TermRTS.Event;
using TermRTS.Storage;
using TermRTS.Ui;

namespace TermRTS.Examples.Greenery.Ui;

internal enum InputState
{
    Idle,
    OngoingInput
}

public class TextBox(SchedulerEventQueue evtQueue) : UiElementBase
{
    #region Fields

    private const ConsoleColor DefaultBg = ConsoleColor.Black;
    private const ConsoleColor DefaultFg = ConsoleColor.Gray;
    private const ConsoleColor ActiveBg = ConsoleColor.DarkGray;

    private readonly char[] _msg = new char[80];
    private int _idx;
    private InputState _state = InputState.Idle;

    #endregion

    #region Properties

    public bool IsOngoingInput => _state == InputState.OngoingInput;

    #endregion

    #region IEventSink Members

    public void HandleKeyInput(in ConsoleKeyInfo keyInfo)
    {
        if (keyInfo.Key == ConsoleKey.Enter)
            switch (_state)
            {
                case InputState.Idle:
                    _state = InputState.OngoingInput;
                    IsRequireRender = true;
                    Array.Clear(_msg, 0, 80);
                    return;
                case InputState.OngoingInput:
                    FinalizeMessage();
                    IsRequireRender = true;
                    return;
                default:
                    return;
            }

        if (!IsOngoingInput) return;

        IsRequireRender = true;

        if (_idx == _msg.Length) return;
        switch (keyInfo.Key)
        {
            case ConsoleKey.Spacebar:
                _msg[_idx] = ' ';
                _idx += 1;
                break;
            case ConsoleKey.Backspace:
                _idx = Math.Max(_idx - 1, 0);
                break;
            case ConsoleKey.LeftArrow:
            case ConsoleKey.RightArrow:
            case ConsoleKey.UpArrow:
            case ConsoleKey.DownArrow:
                break;
            default:
                _msg[_idx] = keyInfo.KeyChar;
                _idx += 1;
                break;
        }
    }

    #endregion

    #region UiElementBase Members

    public override void UpdateSelfFromComponents(
        in IReadonlyStorage componentStorage,
        double timeStepSizeMs,
        double howFarIntoNextFramePercent)
    {
        // Does not require components to work.
    }

    protected override void RenderSelf(RenderContext ctx)
    {
        var fg = DefaultFg;
        var bg = IsOngoingInput ? ActiveBg : DefaultBg;

        // render blank line
        for (var i = 0; i < Width; i += 1)
            ctx.Draw(i, 0, ' ', fg, bg);

        if (!IsOngoingInput) return;

        // render prompt
        ctx.Draw(0, 0, '>', bg, fg);
        ctx.Draw(1, 0, ' ', bg, fg);

        // render text
        var input = GetCurrentInput();
        var startX = 2;
        for (var i = 0; i < _idx; i += 1)
        {
            ctx.Draw(startX + i, 0, input[i], bg, fg);
        }
    }

    protected override void OnXChanged()
    {
        IsRequireRender = true;
    }

    protected override void OnYChanged()
    {
        IsRequireRender = true;
    }

    protected override void OnWidthChanged()
    {
        IsRequireRender = true;
    }

    protected override void OnHeightChanged()
    {
        IsRequireRender = true;
    }

    #endregion

    #region Members

    private void FinalizeMessage()
    {
        _state = InputState.Idle;
        var cmd = new char[_idx];
        if (_idx > 0)
            Array.Copy(_msg, 0, cmd, 0, _idx);
        evtQueue.EnqueueEvent(ScheduledEvent.From(new Event.Command(cmd)));
        _idx = 0;
    }

    private ReadOnlySpan<char> GetCurrentInput()
    {
        return _idx == 0
            ? new ReadOnlySpan<char>(_msg, 0, 0)
            : new ReadOnlySpan<char>(_msg, 0, _idx);
    }

    #endregion
}