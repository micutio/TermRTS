using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using TermRTS.Event;

namespace TermRTS.Io;

/// <summary>
///     Input processing for terminal key events.
/// </summary>
public class ConsoleInput : IEventSink
{
    private readonly Channel<(IEvent, ulong)> _channel;
    private readonly Thread _thread;
    private bool _keepRunning;

    public ConsoleInput(ConsoleKey? terminatorKey = null)
    {
        _channel = Channel.CreateUnbounded<(IEvent, ulong)>();
        _thread = new Thread(ListenForKeyInput);
        TerminatorKey = terminatorKey;
    }

    public ChannelReader<(IEvent, ulong)> KeyEventReader => _channel.Reader;

    #region IEventSink Members

    public void ProcessEvent(IEvent evt)
    {
        if (evt.Type() != EventType.Shutdown) return;

        _keepRunning = false;
        _thread.Join();
    }

    #endregion

    #region Properties

    public ConsoleKey? TerminatorKey { get; set; }

    #endregion

    #region Public API

    public void Run()
    {
        _keepRunning = true;
        _thread.Start();
    }

    #endregion

    private void ListenForKeyInput()
    {
        var timer = new Stopwatch();
        while (_keepRunning)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !Console.KeyAvailable)
            {
                timer.Restart();
                while (timer.ElapsedMilliseconds < 16) Task.Delay(16).Wait();
                continue;
            }

            var keyInfo = Console.ReadKey(true);

            if (TerminatorKey != null && keyInfo.Key == TerminatorKey)
            {
                _keepRunning = false;
                _channel.Writer.TryWrite((new PlainEvent(EventType.Shutdown), 0L));
            }

            FireKeyEvent(keyInfo);
        }

        _channel.Writer.Complete();
    }

    private void FireKeyEvent(ConsoleKeyInfo keyInfo)
    {
        _channel.Writer.TryWrite((new KeyInputEvent(keyInfo), 0L));
    }
}