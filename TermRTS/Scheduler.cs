using System.Diagnostics;
using TermRTS.Event;

namespace TermRTS;

internal record SchedulerState(
    ulong TimeMs,
    List<(IEvent, ulong)> EventQueueItems,
    List<ScheduledEvent> EmittedEvents,
    List<ScheduledEvent> NextTickEvents,
    CoreState CoreState);

public class SchedulerEventQueue
{
    internal readonly EventQueue<IEvent, ulong> Instance = new();

    /// <summary>
    ///     Offers a manual way to schedule an event with an optional due time.
    /// </summary>
    /// <param name="evt">
    ///     A tuple of the event and due-time, given as absolute timestamp in ms
    /// </param>
    public void EnqueueEvent(ScheduledEvent evt)
    {
        if (!Instance.TryAdd((evt.Event, evt.Event.TriggerTime)))
            throw new Exception($"Cannot add event to queue: {evt.Event}");
    }
}

public class Scheduler
{
    #region Fields

    // time constants
    private readonly TimeSpan _msPerUpdate;
    private readonly ulong _timeStepSizeMs;

    // time keeping tools
    private readonly Stopwatch _loopTimer = new();
    private readonly Stopwatch _tickTimer = new();
    private readonly Stopwatch _renderTimer = new();
    private readonly Stopwatch _pauseTimer = new();
    private TimeSpan _lag;

    // statistics recording for profiling
    private readonly Profiler _profiler;

    // Events received from processing the game systems
    private readonly List<ScheduledEvent> _emittedEvents = new(2048);

    // The "Fast Lane" - 90% of traffic
    private readonly List<ScheduledEvent> _nextTickEvents = new(2048);

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
        _lag = TimeSpan.FromMilliseconds(msPerUpdate);
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

    public bool IsActive => _core.IsRunning();

    // The "Slow Lane" - for future-dated events
    public readonly SchedulerEventQueue FutureEvents = new();

    #endregion

    #region Public Methods

    /// <summary>
    ///     Add a new event sink, which will receive events of the specified type.
    /// </summary>
    public void AddEventSink(IEventSink sink, Type payloadType)
    {
        if (!_eventSinks.TryGetValue(payloadType, out var sinks))
        {
            sinks = [];
            _eventSinks[payloadType] = sinks;
        }

        sinks.Add(sink);
    }

    /// <summary>
    ///     Remove an event sink from the scheduler. The given sink will no longer receive any events.
    /// </summary>
    public void RemoveEventSink(IEventSink sink, Type payloadType)
    {
        if (!_eventSinks.TryGetValue(payloadType, out var sinks))
            return;

        sinks.Remove(sink);
        if (sinks.Count == 0)
            _eventSinks.Remove(payloadType);
    }

    public void Prepare()
    {
        if (_core.IsRunning()) _core.SpawnNewEntities();
        _loopTimer.Start();
    }

    /// <summary>
    ///     The core loop for advancing the simulation.
    /// </summary>
    internal void SimulationStep()
    {
        var loopTime = _loopTimer.Elapsed;
        _loopTimer.Restart();
        _lag += loopTime;

        // Reduce possible lag by processing consecutive ticks without rendering
        var tickCount = 0;
        _tickTimer.Restart();
        while (_lag >= _msPerUpdate)
        {
            // STEP 1: INPUT /////////////////////////////////////////////////////////////////////
            ProcessInput();
            if (!_core.IsRunning())
            {
                _core.Shutdown();
                return;
            }

            // STEP 2: UPDATE ////////////////////////////////////////////////////////////////////

            _core.Tick(_timeStepSizeMs, _emittedEvents);

            foreach (var evt in _emittedEvents)
                if (evt.Event.TriggerTime <= TimeMs)
                    _nextTickEvents.Add(evt);
                else
                    FutureEvents.EnqueueEvent(evt);

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


        // Optional profiling ////////////////////////////////////////////////////////
#if DEBUG
        // Record measurements
        var tickTimeMs = tickCount == 0 ? 0.0 : tickElapsed.TotalMilliseconds / tickCount;
        _profiler.AddTickTimeSample(
            Convert.ToUInt64(loopTime.TotalMilliseconds),
            Convert.ToUInt64(tickTimeMs),
            Convert.ToUInt64(renderTime.TotalMilliseconds),
            Convert.ToUInt64(_pauseTimer.Elapsed.TotalMilliseconds)
        );
        // Push out profiling results every 10 samples
        if (_profiler.SampleSize % 10 == 0)
            FutureEvents.EnqueueEvent(
                ScheduledEvent.From(new Profile(_profiler.ToString())));
#endif
    }

    #endregion

    #region Internal Methods for Serialization

    internal SchedulerState GetSchedulerState()
    {
        // Event sources and sinks are not serialized as it is assumed the application
        // is started already with the wiring in place and only needs to restore the
        // component data.
        return new SchedulerState(
            TimeMs,
            FutureEvents.Instance.GetSerializableElements(),
            [.. _emittedEvents],
            [.. _nextTickEvents],
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

        FutureEvents.Instance.Clear();
        foreach (var (eventItem, priority) in schedulerState.EventQueueItems)
            if (!FutureEvents.Instance.TryAdd((eventItem, priority)))
                throw new Exception($"Cannot add event to queue: {eventItem}");

        _emittedEvents.Clear();
        _emittedEvents.AddRange(schedulerState.EmittedEvents);
        _nextTickEvents.Clear();
        _nextTickEvents.AddRange(schedulerState.NextTickEvents);
        _eventSinks.Clear();
    }

    #endregion

    #region Private Methods

    /// <summary>
    ///     Pause execution of the scheduler for a given time period.
    ///     Due to time constraints and context switching <see cref="Thread.Sleep(TimeSpan)" /> and
    ///     <see cref="Task.Delay(TimeSpan)" /> become inaccurate below the TimeSlice size of 15(?).
    ///     For those we use a busy-spinning loop to exactly time the pause.
    /// </summary>
    /// <param name="timeout">Duration to pause.</param>
    private void Pause(TimeSpan timeout)
    {
        _pauseTimer.Restart();

        // The threshold where Thread.Sleep becomes too dangerous.
        // If you used timeBeginPeriod(1), this can safely be 2ms.
        // If you are cross-platform and haven't touched the OS timer, keep it at 15-16ms.
        const double sleepThresholdMs = 15.0;

        // Tier 1: Deep Sleep (Yields CPU entirely, low power)
        // We subtract the threshold to guarantee we wake up BEFORE the timeout.
        var timeToSleepMs = timeout.TotalMilliseconds - sleepThresholdMs;
        if (timeToSleepMs > 0) Thread.Sleep((int)timeToSleepMs);

        // Tier 2: Active Yielding (Low latency, moderate power)
        // The thread says "I don't need the CPU right now, let someone else work, 
        // but put me at the front of the line to wake back up."
        while (timeout.TotalMilliseconds - _pauseTimer.Elapsed.TotalMilliseconds > 0.1)
            Thread.Sleep(1); // alternatively Thread.Sleep(0) or Thread.Yield()

        // Note: Thread.Sleep(0) is also an option here, but Yield() is generally 
        // preferred for tight spin-waiting as it stays on the current processor.
        // Tier 3: The Micro-Spin (Absolute precision, high power)
        // We only do this for the final fraction of a millisecond.
        while (_pauseTimer.Elapsed < timeout)
            // Don't use an empty while(); loop! 
            // SpinWait tells the CPU "I am spinning", emitting a PAUSE instruction 
            // on x86/x64 architectures. This prevents branch prediction penalties 
            // and slightly reduces CPU heat compared to an empty loop.
            Thread.SpinWait(10);

        _pauseTimer.Stop();
    }

    /// <summary>
    ///     Fires events that are due in the current time step by distributing them to all event
    ///     sinks registered to these event types.
    /// </summary>
    private void ProcessInput()
    {
        // CLear out all immediate events.
        foreach (var evt in _nextTickEvents)
        {
            if (!_eventSinks.TryGetValue(evt.Event.EvtType, out var sinks))
                continue;

            foreach (var sink in sinks) sink.ProcessEvent(evt.Event);
        }

        // Drain due events from the future.
        while (FutureEvents.Instance.TryTakeIf(priority => priority <= TimeMs,
                   out var eventItem))
        {
            var (evt, _) = eventItem;
            if (!_eventSinks.TryGetValue(evt.EvtType, out var sinks))
                continue;

            foreach (var sink in sinks)
                sink.ProcessEvent(evt);
        }
    }

    #endregion
}