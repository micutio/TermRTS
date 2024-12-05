using System.Threading.Channels;
using TermRTS.Event;
using TermRTS.Examples.Greenery.Event;

namespace TermRTS.Examples.Greenery;

internal enum InputState
{
    Idle,
    OngoingInput
}

public class TextBox : IEventSink
{
    public ChannelReader<(IEvent, ulong)> MessageEventReader => _channel.Reader;
    private readonly Channel<(IEvent, ulong)> _channel = Channel.CreateUnbounded<(IEvent, ulong)>();
    
    private readonly char[] _msg = new char[80];
    private int _idx;
    private InputState _state = InputState.Idle;
    
    
    public bool IsOngoingInput => _state == InputState.OngoingInput;
    
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
        
        /*
        switch (keyEvent.Info.Key)
        {
            case ConsoleKey.A:
                _msg.Append(isShift ? 'A' : 'a');
                break;
            case ConsoleKey.B:
                _msg.Append(isShift ? 'B' : 'b');
                break;
            case ConsoleKey.C:
                _msg.Append(isShift ? 'C' : 'c');
                break;
            case ConsoleKey.D:
                _msg.Append(isShift ? 'D' : 'd');
                break;
            case ConsoleKey.E:
                _msg.Append(isShift ? 'E' : 'e');
                break;
            case ConsoleKey.F:
                _msg.Append(isShift ? 'F' : 'f');
                break;
            case ConsoleKey.G:
                _msg.Append(isShift ? 'G' : 'g');
                break;
            case ConsoleKey.H:
                _msg.Append(isShift ? 'H' : 'h');
                break;
            case ConsoleKey.I:
                _msg.Append(isShift ? 'I' : 'i');
                break;
            case ConsoleKey.J:
                _msg.Append(isShift ? 'J' : 'j');
                break;
            case ConsoleKey.K:
                _msg.Append(isShift ? 'K' : 'k');
                break;
            case ConsoleKey.L:
                _msg.Append(isShift ? 'L' : 'l');
                break;
            case ConsoleKey.M:
                _msg.Append(isShift ? 'M' : 'm');
                break;
            case ConsoleKey.N:
                _msg.Append(isShift ? 'N' : 'n');
                break;
            case ConsoleKey.O:
                _msg.Append(isShift ? 'O' : 'o');
                break;
            case ConsoleKey.P:
                _msg.Append(isShift ? 'P' : 'p');
                break;
            case ConsoleKey.Q:
                _msg.Append(isShift ? 'Q' : 'q');
                break;
            case ConsoleKey.R:
                _msg.Append(isShift ? 'R' : 'r');
                break;
            case ConsoleKey.S:
                _msg.Append(isShift ? 'S' : 's');
                break;
            case ConsoleKey.T:
                _msg.Append(isShift ? 'T' : 't');
                break;
            case ConsoleKey.U:
                _msg.Append(isShift ? 'U' : 'u');
                break;
            case ConsoleKey.V:
                _msg.Append(isShift ? 'V' : 'v');
                break;
            case ConsoleKey.W:
                _msg.Append(isShift ? 'W' : 'w');
                break;
            case ConsoleKey.X:
                _msg.Append(isShift ? 'X' : 'x');
                break;
            case ConsoleKey.Y:
                _msg.Append(isShift ? 'Y' : 'y');
                break;
            case ConsoleKey.Z:
                _msg.Append(isShift ? 'Z' : 'z');
                break;
            case ConsoleKey.Tab:
                _msg.Append('\t');
                break;
            case ConsoleKey.OemComma:
                _msg.Append(',');
                break;
            case ConsoleKey.NumPad0:
            case ConsoleKey.D0:
                _msg.Append('0');
                break;
            case ConsoleKey.NumPad1:
            case ConsoleKey.D1:
                _msg.Append('1');
                break;
            case ConsoleKey.NumPad2:
            case ConsoleKey.D2:
                _msg.Append('2');
                break;
            case ConsoleKey.NumPad3:
            case ConsoleKey.D3:
                _msg.Append('3');
                break;
            case ConsoleKey.NumPad4:
            case ConsoleKey.D4:
                _msg.Append('4');
                break;
            case ConsoleKey.NumPad5:
            case ConsoleKey.D5:
                _msg.Append('5');
                break;
            case ConsoleKey.NumPad6:
            case ConsoleKey.D6:
                _msg.Append('6');
                break;
            case ConsoleKey.NumPad7:
            case ConsoleKey.D7:
                _msg.Append('7');
                break;
            case ConsoleKey.NumPad8:
            case ConsoleKey.D8:
                _msg.Append('8');
                break;
            case ConsoleKey.NumPad9:
            case ConsoleKey.D9:
                _msg.Append('9');
                break;
            case ConsoleKey.Spacebar:
                _msg.Append(' ');
                break;
                */
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