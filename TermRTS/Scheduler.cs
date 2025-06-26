using System.Diagnostics;
using System.Threading.Channels;
using TermRTS.Event;

namespace TermRTS;

internal record SchedulerState(
    ulong TimeMs,
    List<(IEvent, ulong)> EventQueueItems,
    CoreState CoreState);

public class Scheduler
{
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
    private readonly Channel<ScheduledEvent> _channel = Channel.CreateUnbounded<ScheduledEvent>();

    // serialized via property
    private readonly EventQueue<IEvent, ulong> _eventQueue = new();
    private readonly Dictionary<Type, List<IEventSink>> _eventSinks = new();

    private readonly Core _core;

    #endregion

    #region Constructor

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
        AddEventSink(_core, typeof(Shutdown));

        TimeMs = 0L;
    }

    #endregion

    #region Properties

    /// <summary>
    ///     Property for read-only access to current simulation time.
    /// </summary>
    public ulong TimeMs { get; private set; }

    /// <summary>
    ///     Reader for channel to receive Profile events on.
    /// </summary>
    public ChannelReader<ScheduledEvent> ProfileEventReader => _channel.Reader;

    public bool IsActive => _core.IsRunning();

    #endregion

    #region Public Methods

    /// <summary>
    ///     Add a new event source to the scheduler. Using a channel the event source can send in
    ///     new events asynchronously.
    /// </summary>
    public void AddEventSources(params ChannelReader<ScheduledEvent>[] sources)
    {
        Task.Run(async () => { await Task.WhenAll(sources.Select(Redirect).ToArray()); });
    }

    /// <summary>
    ///     Add a new event sink, which will receive events of the specified type.
    /// </summary>
    public void AddEventSink(IEventSink sink, Type payloadType)
    {
        var isFound = _eventSinks.TryGetValue(payloadType, out var sinks);
        if (!isFound || sinks == null)
            sinks = new List<IEventSink>();

        sinks.Add(sink);
        _eventSinks[payloadType] = sinks;
    }

    /// <summary>
    ///     Remove an event sink from the scheduler. The given sink will no longer receive any events.
    /// </summary>
    public void RemoveEventSink(IEventSink sink, Type payloadType)
    {
        _eventSinks[payloadType].Remove(sink);
    }

    /// <summary>
    ///     This method offers a manual way of schedule
    /// </summary>
    /// <param name="evt">
    ///     A tuple of the event and due-time, given as absolute timestamp in ms
    /// </param>
    public void EnqueueEvent(ScheduledEvent evt)
    {
        if (!_eventQueue.TryAdd((evt.Event, evt.ScheduledTime)))
            throw new Exception($"Cannot add event to queue: {evt.Event}");
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
            _channel.Writer.TryWrite(ScheduledEvent.From(new Profile(_profiler.ToString())));
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
            if (!_eventQueue.TryAdd((eventItem, priority)))
                throw new Exception($"Cannot add event to queue: {eventItem}");
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
        Console.WriteLine($"[Scheduler] Event queue size: {_eventQueue.Count}");
        while (_eventQueue.TryPeek(out _, out var priority) && priority <= TimeMs)
        {
            if (!_eventQueue.TryTake(out var eventItem)) continue;

            if (!_eventSinks.TryGetValue(eventItem.Item1.EvtType, out var sink)) continue;

            foreach (var eventSink in sink)
                eventSink.ProcessEvent(eventItem.Item1);
        }
    }

    /// <summary>
    ///     Redirect event messages from an input ChannelReader directly into the event queue.
    /// </summary>
    /// <param name="input">Channel reader providing event messages</param>
    private async Task Redirect(ChannelReader<ScheduledEvent> input)
    {
        await foreach (var item in input.ReadAllAsync())
        {
            Console.WriteLine($"[Scheduler] Enqueueing event {item}");
            EnqueueEvent(item);
        }
    }

    #endregion
}