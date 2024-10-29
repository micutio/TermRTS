using System.Diagnostics;
using System.Threading.Channels;

namespace TermRTS.IO;

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
    
    public void ProcessEvent(IEvent evt)
    {
        if (evt.Type() != EventType.Shutdown) return;
        
        _keepRunning = false;
        _thread.Join();
    }
    
    public void Run()
    {
        _keepRunning = true;
        _thread.Start();
    }
    
    // TODO: Reduce input checking frequency if there hasn't been any input for a long time.
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
                AdjustWaitingTimeForIntput(elapsedMs);
                _timer.Start(); // Resume without resetting.
                
                Thread.Sleep(_sleepMs);
                continue;
            }
            
            _timer.Restart(); // Reset waiting timer.
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
    
    private void AdjustWaitingTimeForIntput(long lastWaitInMs)
    {
        switch (lastWaitInMs)
        {
            case < 1000:
                _sleepMs = 16;
                return;
            case < 1000 * 60:
                _sleepMs = 250;
                return;
            default:
                _sleepMs = 1000;
                return;
        }
    }
}