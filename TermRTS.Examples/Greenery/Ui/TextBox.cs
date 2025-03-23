using System.Threading.Channels;
using TermRTS.Event;
using TermRTS.Examples.Greenery.Event;

namespace TermRTS.Examples.Greenery.Ui;

internal enum InputState
{
    Idle,
    OngoingInput
}

public class TextBox : IEventSink
{
    #region Private Fields

    private readonly Channel<(IEvent, ulong)> _channel = Channel.CreateUnbounded<(IEvent, ulong)>();

    private readonly char[] _msg = new char[80];
    private int _idx;
    private InputState _state = InputState.Idle;

    #endregion

    #region Properties

    public ChannelReader<(IEvent, ulong)> MessageEventReader => _channel.Reader;

    public bool IsOngoingInput => _state == InputState.OngoingInput;

    #endregion

    public void ProcessEvent(IEvent evt)
    {
        if (evt.Type() != EventType.KeyInput) return;

        var keyEvent = (KeyInputEvent)evt;

        if (keyEvent.Info.Key.Equals(ConsoleKey.Enter))
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

        // var isShift = (keyEvent.Info.Modifiers & ConsoleModifiers.Shift) != 0;
        switch (keyEvent.Info.Key)
        {
            case ConsoleKey.Spacebar:
                _msg[_idx] = ' ';
                _idx += 1;
                break;
            case ConsoleKey.Backspace:
                _idx = Math.Max(_idx - 1, 0);
                break;
            default:
                _msg[_idx] = keyEvent.Info.KeyChar;
                _idx += 1;
                break;
        }
    }

    private void FinalizeMessage()
    {
        _idx = 0;
        _state = InputState.Idle;
        _channel.Writer.TryWrite((new CommandEvent(_msg), 0L));
    }

    public ArraySegment<char> GetCurrentInput()
    {
        return _idx == 0
            ? []
            : new ArraySegment<char>(_msg, 0, _idx);
    }
}