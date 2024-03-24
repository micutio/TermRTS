using System.Threading.Channels;

namespace TermRTS.IO;

/// <summary>
/// Input processing for terminal key events.
/// </summary>
public class ConsoleInput : IEventSink
{
    private readonly Channel<(IEvent, UInt64)> _channel;
    private readonly Thread _thread;
    private bool _keepRunning;

    public ConsoleInput()
    {
        _channel = Channel.CreateUnbounded<(IEvent, UInt64)>();
        _thread = new Thread(ListenForKeyInput);
    }

    public ChannelReader<(IEvent, UInt64)> KeyEventReader => _channel.Reader;

    public void Run()
    {
        _keepRunning = true;
        _thread.Start();
    }

    private void ListenForKeyInput()
    {
        while (_keepRunning)
        {
            var keyInfo = Console.ReadKey(true);
            FireKeyEvent(keyInfo);

            // if (keyInfo.Key == ConsoleKey.Escape)
            //    _keepRunning = false;
        }
        _channel.Writer.Complete();
    }

    private void FireKeyEvent(ConsoleKeyInfo keyInfo)
    {
        _channel.Writer.TryWrite((new KeyInputEvent(keyInfo), 0L));
    }

    public void ProcessEvent(IEvent evt)
    {
        if (evt.Type() == EventType.Shutdown) _keepRunning = false;
    }
}