using System.Diagnostics;
using System.Threading.Channels;
using TermRTS.Event;

namespace TermRTS;

internal record SchedulerState(
    ulong TimeMs,
    List<(IEvent, ulong)> EventQueueItems,
    CoreState CoreState
)
{
}

public class Scheduler : IEventSink
{
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

    #endregion

    #region IEventSink Members

    public void ProcessEvent(IEvent evt)
    {
    }

    #endregion

    #region Public Methods

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

    #region Internal Methods for Serialization

    internal SchedulerState GetSchedulerState()
    {
        return new SchedulerState(
            TimeMs,
            _eventQueue.GetSerializableElements(),
            _core.GetSerializableCoreState()
        );
    }

    internal void ReplaceSchedulerState(SchedulerState schedulerState)
    {
        TimeMs = schedulerState.TimeMs;
        _core.ReplaceCoreState(schedulerState.CoreState);
        _loopTimer.Reset();
        _tickTimer.Reset();
        _renderTimer.Reset();
        _pauseTimer.Reset();

        _eventQueue.Clear();
        foreach (var (eventItem, priority) in schedulerState.EventQueueItems)
            _eventQueue.TryAdd((eventItem, priority));
    }

    #endregion

    #region Private Methods

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

    #region Fields

    private static readonly TimeSpan TimeResolution = TimeSpan.FromMilliseconds(100);

    // time constants
    private readonly TimeSpan _msPerUpdate;
    private readonly ulong _timeStepSizeMs;

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
    private readonly Channel<(IEvent, ulong)> _channel = Channel.CreateUnbounded<(IEvent, ulong)>();

    // serialized via property
    private readonly EventQueue<IEvent, ulong> _eventQueue = new();
    private readonly Dictionary<EventType, List<IEventSink>> _eventSinks = new();

    private readonly Core _core;

    #endregion

    #region Properties

    /// <summary>
    ///     Property for read-only access to current simulation time.
    /// </summary>
    public ulong TimeMs { get; private set; }

    /// <summary>
    ///     Reader for channel to receive Profile events on.
    /// </summary>
    public ChannelReader<(IEvent, ulong)> ProfileEventReader => _channel.Reader;

    public bool IsActive => _core.IsRunning();

    #endregion
}