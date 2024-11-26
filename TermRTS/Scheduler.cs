using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TermRTS.Events;

namespace TermRTS;

public class Scheduler : IEventSink
{
    #region Private Fields
    
    private static readonly TimeSpan TimeResolution = TimeSpan.FromMilliseconds(100);
    
    // channel for emitting events
    private readonly Channel<(IEvent, ulong)> _channel;
    
    private readonly Profiler _profiler;
    private readonly TimeSpan _msPerUpdate;
    private readonly ulong _timeStepSizeMs;
    
    private readonly Stopwatch _loopTimer;
    private readonly Stopwatch _tickTimer;
    private readonly Stopwatch _renderTimer;
    
    private readonly EventQueue<IEvent, ulong> _eventQueue;
    private readonly Dictionary<EventType, List<IEventSink>> _eventSinks;
    private readonly ICore _core;
    
    private readonly Stopwatch _pauseTimer;
    
    #endregion
    
    #region Constructor
    
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
        _loopTimer = new Stopwatch();
        _tickTimer = new Stopwatch();
        _renderTimer = new Stopwatch();
        _eventQueue = new EventQueue<IEvent, ulong>();
        _eventSinks = new Dictionary<EventType, List<IEventSink>>();
        _channel = Channel.CreateUnbounded<(IEvent, ulong)>();
        _pauseTimer = new Stopwatch();
        
        _core = core;
        AddEventSink(_core, EventType.Shutdown);
        
        TimeMs = 0L;
    }
    
    #endregion
    
    #region IEventSink Members
    
    public void ProcessEvent(IEvent evt)
    {
    }
    
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
        var lag = TimeSpan.Zero;
        
        if (_core.IsRunning()) _core.SpawnNewEntities();
        
        _loopTimer.Start();
        while (_core.IsRunning())
        {
            var loopTime = _loopTimer.Elapsed;
            _loopTimer.Restart();
            lag += loopTime;
            
            // STEP 1: INPUT /////////////////////////////////////////////////////////////////////
            ProcessInput();
            if (!_core.IsRunning())
            {
                _core.Shutdown();
                break;
            }
            
            // STEP 2: UPDATE ////////////////////////////////////////////////////////////////////
            // Reduce possible lag by processing consecutive ticks without rendering
            var tickCount = 0;
            _tickTimer.Restart();
            while (lag >= _msPerUpdate)
            {
                _core.Tick(_timeStepSizeMs);
                TimeMs += _timeStepSizeMs;
                lag -= _msPerUpdate;
                tickCount += 1;
            }
            
            var tickElapsed = _tickTimer.Elapsed;
            
            // STEP 3: RENDER ////////////////////////////////////////////////////////////////////
            _renderTimer.Restart();
            var howFarIntoNextFramePercent = lag.TotalMilliseconds / _msPerUpdate.TotalMilliseconds;
            _core.Render(_timeStepSizeMs, howFarIntoNextFramePercent);
            
            var tickTimeMs = tickCount == 0 ? 0.0 : tickElapsed.TotalMilliseconds / tickCount;
            var renderTime = _renderTimer.Elapsed;
            // Record times for profiling
            _profiler.AddTickTimeSample(
                Convert.ToUInt64(loopTime.TotalMilliseconds),
                Convert.ToUInt64(tickTimeMs),
                Convert.ToUInt64(renderTime.TotalMilliseconds));
            // Push out profiling results every 10 samples
#if DEBUG
            // if (_profiler.SampleSize % 10 == 0)
            //     _channel.Writer.TryWrite((new ProfileEvent(_profiler.ToString()), 0L));
#endif
            
            // Get loop time after making up for previous lag
            var caughtUpLoopTime = lag + renderTime + tickElapsed;
            
            // If we spent longer than our allotted time, skip right ahead...
            if (caughtUpLoopTime >= _msPerUpdate) continue;
            
            // ...otherwise wait until the next frame is due.
            Pause(_msPerUpdate - caughtUpLoopTime);
        }
        
        _channel.Writer.Complete();
    }
    
    #endregion
    
    #region Private Members
    
    /// <summary>
    ///     Pause execution of the scheduler for a given time period.
    ///     Due to time constraints and context switching <see cref="Thread.Sleep(TimeSpan)" /> and
    ///     <see cref="Task.Delay(TimeSpan)" /> become inaccurate below the TimeSlice size of 15(?)/>
    /// </summary>
    /// <param name="timeout"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Pause(TimeSpan timeout)
    {
        if (timeout > TimeResolution)
        {
            Thread.Sleep(timeout);
            return;
        }
        
        _pauseTimer.Restart();
        while (_pauseTimer.Elapsed < timeout) ;
        _pauseTimer.Stop();
    }
    
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