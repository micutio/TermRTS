using System.Threading.Channels;

namespace termRTS.Runner;

public class Input
{
    private readonly Channel<string> _channel;
    private readonly Thread _thread;
    private bool _keepRunning;

    public Input()
    {
        _channel = Channel.CreateUnbounded<string>();
        _thread = new Thread(ListenForKeyInput);
    }

    public ChannelReader<string> KeyEventReader => _channel.Reader;

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

            if (keyInfo.Key == ConsoleKey.Escape)
                _keepRunning = false;
        }
        _channel.Writer.TryWrite("input shutting down");
        _channel.Writer.Complete();
    }

    private void FireKeyEvent(ConsoleKeyInfo keyInfo)
    {
        _channel.Writer.TryWrite($"key {keyInfo.KeyChar}");
    }
}
