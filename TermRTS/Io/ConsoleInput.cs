using System.Diagnostics;
using System.Threading.Channels;
using TermRTS.Event;
using TermRTS.Events;

namespace TermRTS.Io;

/// <summary>
///     Input processing for terminal key events.
/// </summary>
public class ConsoleInput : IEventSink
{
    private readonly Channel<(IEvent, ulong)> _channel;
    private readonly Thread _thread;
    private readonly Stopwatch _timer;
    private bool _keepRunning;
    private int _sleepMs = 16;
    
    public ConsoleInput()
    {
        _channel = Channel.CreateUnbounded<(IEvent, ulong)>();
        _thread = new Thread(ListenForKeyInput);
        _timer = new Stopwatch();
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
    
    public void Run()
    {
        _keepRunning = true;
        _thread.Start();
    }
    
    private void ListenForKeyInput()
    {
        _timer.Start();
        while (_keepRunning)
        {
            if (!Console.KeyAvailable)
            {
                // Check how long we have been waiting so far.
                // If there are longer periods of waiting, then adjust to check less frequently.
                _timer.Stop();
                var elapsedMs = _timer.ElapsedMilliseconds;
                AdjustWaitingTimeForInput(elapsedMs);
                _timer.Start(); // Resume without resetting.
                
                Thread.Sleep(_sleepMs);
                continue;
            }
            
            _timer.Restart(); // Reset waiting timer.
            var keyInfo = Console.ReadKey(true);
            FireKeyEvent(keyInfo);
        }
        
        _channel.Writer.Complete();
    }
    
    private void FireKeyEvent(ConsoleKeyInfo keyInfo)
    {
        _channel.Writer.TryWrite((new KeyInputEvent(keyInfo), 0L));
    }
    
    private void AdjustWaitingTimeForInput(long lastWaitInMs)
    {
        switch (lastWaitInMs)
        {
            case < 500:
                _sleepMs = 10;
                return;
            case < 1000:
                _sleepMs = 250;
                return;
            case < 1000 * 30:
                _sleepMs = 500;
                return;
            default:
                _sleepMs = 1000;
                return;
        }
    }
}