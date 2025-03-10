using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using TermRTS.Event;
using TermRTS.Events;

namespace TermRTS;

internal record SchedulerState(
    ulong TimeMs,
    TimeSpan MsPerUpdate,
    ulong TimeStepSizeMs,
    Stopwatch LoopTimer,
    Stopwatch TickTimer,
    Stopwatch RenderTimer,
    Stopwatch PauseTimer,
    TimeSpan Lag,
    EventQueue<IEvent, ulong> EventQueue,
    Dictionary<EventType, List<IEventSink>> EventSinks,
    Core Core
)
{
}

public class Scheduler : IEventSink
{
    #region Private Fields
    
    private static readonly TimeSpan TimeResolution = TimeSpan.FromMilliseconds(100);
    
    // time constants
    private readonly TimeSpan _msPerUpdate = TimeSpan.FromMilliseconds(16.0d);
    private readonly ulong _timeStepSizeMs = 16L;
    
    // time keeping tools
    private readonly Stopwatch _loopTimer = new();
    private readonly Stopwatch _tickTimer = new();
    private readonly Stopwatch _renderTimer = new();
    private readonly Stopwatch _pauseTimer = new();
    private TimeSpan _lag = TimeSpan.Zero;
    
    // statistics recording for profiling
    private readonly Profiler _profiler;
    
    // the meaty bits - actual simulation loop logic
    
    // channel for emitting events
    // TODO: Does this need to be serialized?
    private readonly Channel<(IEvent, ulong)> _channel = Channel.CreateUnbounded<(IEvent, ulong)>();
    
    // serialized via property
    private readonly EventQueue<IEvent, ulong> _eventQueue = new();
    private readonly Dictionary<EventType, List<IEventSink>> _eventSinks = new();
    
    private readonly Core _core;
    
    #endregion
    
    #region Constructors
    
    /// <summary>
    ///     Constructor.
    /// </summary>
    /// <param name="msPerUpdate">How much time is allocated for processing each frame</param>
    /// <param name="timeStepSizeMs">How much time is processed during one simulation tick</param>
    /// <param name="core">Game core object, which is performing the actual simulation ticks</param>
    public Scheduler(Core core, double msPerUpdate = 16.0d, ulong timeStepSizeMs = 16L)
    {
        _profiler = new Profiler(timeStepSizeMs);
        _msPerUpdate = TimeSpan.FromMilliseconds(msPerUpdate);
        _timeStepSizeMs = timeStepSizeMs;
        _core = core;
        AddEventSink(_core, EventType.Shutdown);
        
        TimeMs = 0L;
    }
    
    /// <summary>
    /// Constructor for deserialisation from JSON.
    /// </summary>
    [JsonConstructor]
    internal Scheduler(SchedulerState schedulerState)
    {
        TimeMs = schedulerState.TimeMs;
        _core = schedulerState.Core;
        _msPerUpdate = schedulerState.MsPerUpdate;
        _timeStepSizeMs = schedulerState.TimeStepSizeMs;
        _loopTimer = schedulerState.LoopTimer;
        _tickTimer = schedulerState.TickTimer;
        _renderTimer = schedulerState.RenderTimer;
        _pauseTimer = schedulerState.PauseTimer;
        _lag = schedulerState.Lag;
        _eventSinks = schedulerState.EventSinks;
        
        _profiler = new Profiler(_timeStepSizeMs);
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
    [JsonIgnore]
    public ulong TimeMs { get; private set; }
    
    /// <summary>
    ///     Reader for channel to receive Profile events on.
    /// </summary>
    [JsonIgnore]
    public ChannelReader<(IEvent, ulong)> ProfileEventReader => _channel.Reader;
    
    [JsonIgnore] public bool IsActive => _core.IsRunning();
    
    [JsonInclude]
    internal SchedulerState SchedulerState =>
        new(
            TimeMs,
            _msPerUpdate,
            _timeStepSizeMs,
            _loopTimer,
            _tickTimer,
            _renderTimer,
            _pauseTimer,
            _lag,
            _eventQueue,
            _eventSinks,
            _core
        );
    
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
    
    public void Prepare()
    {
        if (_core.IsRunning()) _core.SpawnNewEntities();
        _loopTimer.Start();
    }
    
    /// <summary>
    ///     The core loop for advancing the simulation.
    /// </summary>
    public void SimulationStep()
    {
        var loopTime = _loopTimer.Elapsed;
        _loopTimer.Restart();
        _lag += loopTime;
        
        // STEP 1: INPUT /////////////////////////////////////////////////////////////////////
        ProcessInput();
        if (!_core.IsRunning())
        {
            _core.Shutdown();
            return;
        }
        
        // STEP 2: UPDATE ////////////////////////////////////////////////////////////////////
        // Reduce possible lag by processing consecutive ticks without rendering
        var tickCount = 0;
        _tickTimer.Restart();
        while (_lag >= _msPerUpdate)
        {
            _core.Tick(_timeStepSizeMs);
            TimeMs += _timeStepSizeMs;
            _lag -= _msPerUpdate;
            tickCount += 1;
        }
        
        var tickElapsed = _tickTimer.Elapsed;
        
        // STEP 3: RENDER ////////////////////////////////////////////////////////////////////
        _renderTimer.Restart();
        var howFarIntoNextFramePercent = _lag.TotalMilliseconds / _msPerUpdate.TotalMilliseconds;
        _core.Render(_timeStepSizeMs, howFarIntoNextFramePercent);
        
        var renderTime = _renderTimer.Elapsed;
        
        // Step 4: Optional pausing for consistent tick times ////////////////////////////////
        // Get loop time after making up for previous lag
        var caughtUpLoopTime = _lag + renderTime + tickElapsed;
        
        // If we spent longer than our allotted time, skip right ahead...
        if (caughtUpLoopTime >= _msPerUpdate) return;
        
        // ...otherwise wait until the next frame is due.
        Pause(_msPerUpdate - caughtUpLoopTime);
        
        // Step 5: Optional profiling ////////////////////////////////////////////////////////
#if DEBUG
        // Record measurements
        var tickTimeMs = tickCount == 0 ? 0.0 : tickElapsed.TotalMilliseconds / tickCount;
        _profiler.AddTickTimeSample(
            Convert.ToUInt64(loopTime.TotalMilliseconds),
            Convert.ToUInt64(tickTimeMs),
            Convert.ToUInt64(renderTime.TotalMilliseconds));
        // Push out profiling results every 10 samples
        if (_profiler.SampleSize % 10 == 0)
            _channel.Writer.TryWrite((new ProfileEvent(_profiler.ToString()), 0L));
#endif
    }
    
    public void Shutdown()
    {
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
    
    /// <summary>
    ///     Redirect event messages from an input ChannelReader directly into the event queue.
    /// </summary>
    /// <param name="input">Channel reader providing event messages</param>
    private async Task Redirect(ChannelReader<(IEvent, ulong)> input)
    {
        await foreach (var item in input.ReadAllAsync())
            _eventQueue.TryAdd(item);
    }
    
    #endregion
}