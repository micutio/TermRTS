using System.Threading.Channels;

namespace TermRTS.IO;

/// <summary>
///     Input processing for terminal key events.
/// </summary>
public class ConsoleInput : IEventSink
{
    private readonly Channel<(IEvent, ulong)> _channel;
    private readonly Thread _thread;
    private bool _keepRunning;

    public ConsoleInput()
    {
        _channel = Channel.CreateUnbounded<(IEvent, ulong)>();
        _thread = new Thread(ListenForKeyInput);
    }

    public ChannelReader<(IEvent, ulong)> KeyEventReader => _channel.Reader;

    public void ProcessEvent(IEvent evt)
    {
        if (evt.Type() == EventType.Shutdown)
        {
            _keepRunning = false;
            _thread.Join();
        }
    }

    public void Run()
    {
        _keepRunning = true;
        _thread.Start();
    }

    // TODO: Reduce input checking frequency if there hasn't been any input for a long time.
    private void ListenForKeyInput()
    {
        while (_keepRunning)
        {
            if (!Console.KeyAvailable)
            {
                Thread.Sleep(250);
                continue;
            }

            var keyInfo = Console.ReadKey(true);
            FireKeyEvent(keyInfo);
        }

        _channel.Writer.Complete();
        Console.WriteLine("ConsoleInput shut down");
    }

    private void FireKeyEvent(ConsoleKeyInfo keyInfo)
    {
        _channel.Writer.TryWrite((new KeyInputEvent(keyInfo), 0L));
    }
}