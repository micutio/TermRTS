using System.Diagnostics;
using System.Threading.Channels;

namespace TermRTS;

public class Scheduler
{
    private readonly Profiler _profiler;
    private readonly TimeSpan _msPerUpdate;
    private readonly UInt64 _timeStepSizeMs;
    private UInt64 _timeMs;
    private readonly Stopwatch _stopwatch;
    private readonly EventQueue<IEvent, UInt64> _eventQueue;
    private readonly Dictionary<EventType, List<IEventSink>> _eventSinks;
    private readonly ICore _core;

    public UInt64 TimeMs => _timeMs;

    public Scheduler(double frameTimeMs, UInt64 timeStepSizeMs, ICore core)
    {
        _profiler = new Profiler(timeStepSizeMs);
        _msPerUpdate = TimeSpan.FromMilliseconds(frameTimeMs);
        _timeStepSizeMs = timeStepSizeMs;
        _timeMs = 0L;
        _stopwatch = new Stopwatch();
        _eventQueue = new EventQueue<IEvent, UInt64>();
        _eventSinks = new Dictionary<EventType, List<IEventSink>>();
        _core = core;

        Console.WriteLine($"[Scheduler] ms per update: {_msPerUpdate}");
    }

    public void AddEventSources(params ChannelReader<(IEvent, UInt64)>[] sources)
    {
        Task.Run(async () =>
        {
            await Task.WhenAll(sources.Select(Redirect).ToArray());
            return;

            async Task Redirect(ChannelReader<(IEvent, UInt64)> input)
            {
                await foreach (var item in input.ReadAllAsync())
                    _eventQueue.TryAdd(item);
            }
        });
    }

    public void AddEventSink(IEventSink sink, EventType type)
    {
        var isFound = _eventSinks.TryGetValue(type, out var sinks);
        if (!isFound) sinks = new List<IEventSink>();

        if (sinks == null) return;

        sinks.Add(sink);
        _eventSinks[type] = sinks;
    }

    public void RemoveEventSink(IEventSink sink, EventType type)
    {
        _eventSinks[type].Remove(sink);
    }

    public void QueueEvent((IEvent, UInt64) item)
    {
        _eventQueue.TryAdd(item);
    }

    public void ProcessInput()
    {
        while (_eventQueue.TryPeek(out var item, out var priority) && priority <= _timeMs)
        {
            _eventQueue.TryTake(out var eventItem);

            // TODO: makes this cleaner!
            if (eventItem.Item1.Type() == EventType.Profile)
            {
                Console.WriteLine(_profiler.ToString());
            }

            if (!_eventSinks.ContainsKey(eventItem.Item1.Type())) continue;

            foreach (var eventSink in _eventSinks[eventItem.Item1.Type()])
            {
                eventSink.ProcessEvent(eventItem.Item1);
            }
        }
    }

    public void GameLoop()
    {
        _stopwatch.Start();
        var lag = TimeSpan.FromMilliseconds((double)_timeStepSizeMs);

        while (_core.IsGameRunning())
        {
            _stopwatch.Stop();
            lag += _stopwatch.Elapsed;

            Console.WriteLine($"[Scheduler Gameloop] current time: {_timeMs}, lag: {lag.TotalMilliseconds}");
            _stopwatch.Restart();

            // STEP 1: INPUT
            ProcessInput();
            if (!_core.IsGameRunning()) break;

            // STEP 2: UPDATE
            while (lag >= _msPerUpdate)
            {
                // Console.WriteLine("TICK");
                _core.Tick(_timeStepSizeMs);
                _timeMs += _timeStepSizeMs;
                lag -= _msPerUpdate;
            }

            // STEP 3: RENDER
            var howFarIntoNextFrameMs = lag.TotalMilliseconds / _msPerUpdate.TotalMilliseconds;
            var renderWatch = Stopwatch.StartNew();
            _core.Render(howFarIntoNextFrameMs);
            renderWatch.Stop();
            Console.WriteLine($"[Scheduler Gameloop] render duration: {renderWatch.Elapsed}");
            var renderElapsed = renderWatch.Elapsed;

            // Take a break if we're ahead of time.
            var loopTimeMs = lag + renderElapsed;
            _profiler.AddTickTimeSample((UInt64)loopTimeMs.TotalMilliseconds, (UInt64)renderElapsed.TotalMilliseconds);

            // If we spent longer than our allotted time, skip right ahead...
            Console.WriteLine($"loop active time: {loopTimeMs.TotalMilliseconds}");
            if (loopTimeMs >= _msPerUpdate)
                continue;

            // ...otherwise wait until the next frame is due.
            var sleepyTime = (_msPerUpdate - loopTimeMs).TotalMilliseconds;
            Console.WriteLine($"[Scheduler Gameloop] pausing game loop for {sleepyTime} ms");
            Thread.Sleep((int)sleepyTime);
        }
    }
}
