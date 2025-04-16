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
    private readonly Channel<ScheduledEvent> _channel;
    private readonly Thread _thread;
    private bool _keepRunning;

    public ConsoleInput(ConsoleKey? terminatorKey = null)
    {
        _channel = Channel.CreateUnbounded<ScheduledEvent>();
        _thread = new Thread(ListenForKeyInput);
        TerminatorKey = terminatorKey;
    }

    public ChannelReader<ScheduledEvent> KeyEventReader => _channel.Reader;

    #region Properties

    public ConsoleKey? TerminatorKey { get; set; }

    #endregion

    #region IEventSink Members

    public void ProcessEvent(IEvent evt)
    {
        if (evt is not Event<Shutdown>) return;

        _keepRunning = false;
        _thread.Join();
    }

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
                _channel.Writer.TryWrite(ScheduledEvent.From(new Shutdown()));
            }

            FireKeyEvent(keyInfo);
        }

        _channel.Writer.Complete();
    }

    private void FireKeyEvent(ConsoleKeyInfo keyInfo)
    {
        _channel.Writer.TryWrite(ScheduledEvent.From(keyInfo));
    }
}