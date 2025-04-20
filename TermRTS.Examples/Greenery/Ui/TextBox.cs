using System.Threading.Channels;
using TermRTS.Event;

namespace TermRTS.Examples.Greenery.Ui;

internal enum InputState
{
    Idle,
    OngoingInput
}

public class TextBox : IEventSink
{
    #region IEventSink Members

    public void ProcessEvent(IEvent evt)
    {
        if (evt is not Event<ConsoleKeyInfo>(var keyEvent)) return;

        if (keyEvent.Key.Equals(ConsoleKey.Enter))
            switch (_state)
            {
                case InputState.Idle:
                    _state = InputState.OngoingInput;
                    Array.Clear(_msg, 0, 80);
                    return;
                case InputState.OngoingInput:
                    FinalizeMessage();
                    _state = InputState.Idle;
                    return;
            }

        if (!IsOngoingInput) return;

        switch (keyEvent.Key)
        {
            case ConsoleKey.Spacebar:
                _msg[_idx] = ' ';
                _idx += 1;
                break;
            case ConsoleKey.Backspace:
                _idx = Math.Max(_idx - 1, 0);
                break;
            default:
                _msg[_idx] = keyEvent.KeyChar;
                _idx += 1;
                break;
        }
    }

    #endregion

    private void FinalizeMessage()
    {
        _idx = 0;
        _state = InputState.Idle;
        _channel.Writer.TryWrite(ScheduledEvent.From(new Event.Command(_msg)));
    }

    public ReadOnlySpan<char> GetCurrentInput()
    {
        return _idx == 0
            ? new ReadOnlySpan<char>(_msg, 0, 0)
            : new ReadOnlySpan<char>(_msg, 0, _idx);
    }

    #region Fields

    private readonly Channel<ScheduledEvent> _channel = Channel.CreateUnbounded<ScheduledEvent>();

    private readonly char[] _msg = new char[80];
    private int _idx;
    private InputState _state = InputState.Idle;

    #endregion

    #region Properties

    public ChannelReader<ScheduledEvent> MessageEventReader => _channel.Reader;

    public bool IsOngoingInput => _state == InputState.OngoingInput;

    #endregion
}