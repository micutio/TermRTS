using System.Diagnostics;
using System.Threading.Channels;

namespace TermRTS;

public class Scheduler : IEventSink
{
    // channel for emitting events
    // TODO: Replace `(IEvent, UInt64)` with actual type.
    private readonly Channel<(IEvent, ulong)> _channel;
    
    #region Constructor
    
    // TODO: Test time step size 0
    // TODO: Add logging to file
    /// <summary>
    ///     Constructor.
    /// </summary>
    /// <param name="frameTimeMs">How much time is allocated for processing each frame</param>
    /// <param name="timeStepSizeMs">How much time is processed during one simulation tick</param>
    /// <param name="core">Game core object, which is performing the actual simulation ticks</param>
    public Scheduler(double frameTimeMs, ulong timeStepSizeMs, ICore core)
    {
        _profiler = new Profiler(timeStepSizeMs);
        _msPerUpdate = TimeSpan.FromMilliseconds(frameTimeMs);
        _timeStepSizeMs = timeStepSizeMs;
        TimeMs = 0L;
        _stopwatch = new Stopwatch();
        _eventQueue = new EventQueue<IEvent, ulong>();
        _eventSinks = new Dictionary<EventType, List<IEventSink>>();
        _core = core;
        
        _channel = Channel.CreateUnbounded<(IEvent, ulong)>();
    }
    
    #endregion
    
    #region IEventSink Members
    
    public void ProcessEvent(IEvent evt)
    {
        // Emit regular profiling output
        if (evt.Type() == EventType.Profile) Console.WriteLine(_profiler.ToString());
    }
    
    #endregion
    
    #region Private Fields
    
    private readonly Profiler _profiler;
    private readonly TimeSpan _msPerUpdate;
    private readonly ulong _timeStepSizeMs;
    private readonly Stopwatch _stopwatch;
    private readonly EventQueue<IEvent, ulong> _eventQueue;
    private readonly Dictionary<EventType, List<IEventSink>> _eventSinks;
    private readonly ICore _core;
    
    #endregion
    
    #region Properties
    
    /// <summary>
    ///     Property for read-only access to current simulation time.
    /// </summary>
    public ulong TimeMs { get; private set; }
    
    public ChannelReader<(IEvent, ulong)> ProfileEventReader => _channel.Reader;
    
    #endregion
    
    #region Public Members
    
    /// <summary>
    ///     Add a new event source to the scheduler. Using a channel the event source can send in new
    ///     events in an asynchronous fashion.
    /// </summary>
    public void AddEventSources(params ChannelReader<(IEvent, ulong)>[] sources)
    {
        Task.Run(async () => { await Task.WhenAll(sources.Select(Redirect).ToArray()); });
    }
    
    /// <summary>
    ///     Add a new event sink, which will receive events of the specified type.
    /// </summary>
    public void AddEventSink(IEventSink sink, EventType type)
    {
        var isFound = _eventSinks.TryGetValue(type, out var sinks);
        if (!isFound || sinks == null)
            sinks = new List<IEventSink>();
        
        sinks.Add(sink);
        _eventSinks[type] = sinks;
    }
    
    /// <summary>
    ///     Remove an event sink from the scheduler. The given sink will no longer receive any events.
    /// </summary>
    public void RemoveEventSink(IEventSink sink, EventType type)
    {
        _eventSinks[type].Remove(sink);
    }
    
    /// <summary>
    ///     This method offers a manual way of schedule
    /// </summary>
    /// <param name="item">
    ///     A tuple of the event and due-time, given as absolute timestamp in ms
    /// </param>
    public void EnqueueEvent((IEvent, ulong) item)
    {
        _eventQueue.TryAdd(item);
    }
    
    /// <summary>
    ///     The core loop for advancing the simulation.
    /// </summary>
    public void SimulationLoop()
    {
        _stopwatch.Start();
        var lag = TimeSpan.FromMilliseconds(_timeStepSizeMs);
        
        while (_core.IsRunning())
        {
            _stopwatch.Stop();
            lag += _stopwatch.Elapsed;
            
            // Console.WriteLine($"current time: {_timeMs}, lag: {lag.TotalMilliseconds}");
            _stopwatch.Restart();
            
            // STEP 1: INPUT
            ProcessInput();
            if (!_core.IsRunning())
            {
                _core.Shutdown();
                break;
            }
            
            // STEP 2: UPDATE
            while (lag >= _msPerUpdate)
            {
                _core.Tick(_timeStepSizeMs);
                TimeMs += _timeStepSizeMs;
                lag -= _msPerUpdate;
            }
            
            // STEP 3: RENDER
            var howFarIntoNextFrameMs = lag.TotalMilliseconds / _msPerUpdate.TotalMilliseconds;
            var renderWatch = Stopwatch.StartNew();
            _core.Render(howFarIntoNextFrameMs, _timeStepSizeMs);
            renderWatch.Stop();
            // Console.WriteLine($"render duration: {renderWatch.Elapsed}");
            var renderElapsed = renderWatch.Elapsed;
            
            // Take a break if we're ahead of time.
            var loopTimeMs = lag + renderElapsed;
            _profiler.AddTickTimeSample((ulong)loopTimeMs.TotalMilliseconds,
                (ulong)renderElapsed.TotalMilliseconds);
            // Push out profiling results ever 25 samples
            if (_profiler.SampleSize % 5 == 0)
                _channel.Writer.TryWrite((new ProfileEvent(_profiler.ToString()), 0L));
            
            // If we spent longer than our allotted time, skip right ahead...
            // Console.WriteLine($"loop active time: {loopTimeMs.TotalMilliseconds}");
            if (loopTimeMs >= _msPerUpdate)
                continue;
            
            // ...otherwise wait until the next frame is due.
            var sleepyTime = (int)Math.Round((_msPerUpdate - loopTimeMs).TotalMilliseconds, 0,
                MidpointRounding.ToPositiveInfinity);
            // Console.WriteLine($"pausing game loop for {sleepyTime} ms ---------------");
            Thread.Sleep(sleepyTime);
        }
        
        _channel.Writer.Complete();
        Console.WriteLine("Scheduler shut down");
    }
    
    #endregion
    
    #region Private Members
    
    /// <summary>
    ///     Fires events that are due in the current time step by distributing them to all event
    ///     sinks registered to these event types.
    /// </summary>
    private void ProcessInput()
    {
        while (_eventQueue.TryPeek(out _, out var priority) && priority <= TimeMs)
        {
            _eventQueue.TryTake(out var eventItem);
            
            if (!_eventSinks.ContainsKey(eventItem.Item1.Type())) continue;
            
            foreach (var eventSink in _eventSinks[eventItem.Item1.Type()])
                eventSink.ProcessEvent(eventItem.Item1);
        }
    }
    
    private async Task Redirect(ChannelReader<(IEvent, ulong)> input)
    {
        await foreach (var item in input.ReadAllAsync())
            _eventQueue.TryAdd(item);
    }
    
    #endregion
}