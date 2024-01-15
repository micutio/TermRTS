using System.Diagnostics;
using System.Threading.Channels;

namespace TermRTS;

public class Scheduler
{
    private readonly TimeSpan _msPerUpdate;
    private readonly UInt128 _timeStepSizeMs;
    private UInt128 _timeMs;
    private readonly Stopwatch _stopwatch;
    private readonly EventQueue<IEvent, UInt128> _eventQueue;
    private readonly Dictionary<EventType, List<IEventSink>> _eventSinks;
    private readonly ICore _core;

    public UInt128 TimeMs => _timeMs;

    public Scheduler(double frameTimeMs, UInt128 timeStepSizeMs, ICore core)
    {
        _msPerUpdate = TimeSpan.FromSeconds(frameTimeMs);
        _timeStepSizeMs = timeStepSizeMs;
        _timeMs = 0L;
        _stopwatch = new Stopwatch();
        _eventQueue = new EventQueue<IEvent, UInt128>();
        _eventSinks = new Dictionary<EventType, List<IEventSink>>();
        _core = core;
    }

    public void AddEventSources(params ChannelReader<(IEvent, UInt128)>[] sources)
    {
        Task.Run(async () =>
        {
            await Task.WhenAll(sources.Select(Redirect).ToArray());
            return;

            async Task Redirect(ChannelReader<(IEvent, UInt128)> input)
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

    public void ProcessInput()
    {
        while (_eventQueue.Count > 0 && _eventQueue.First().Item2 <= _timeMs)
        {
            _eventQueue.TryTake(out var item);
            foreach (var eventSink in _eventSinks[item.Item1.getType()])
            {
                eventSink.ProcessEvent(item.Item1);
            }
        }
    }

    public void GameLoop()
    {
        _stopwatch.Start();
        var lag = TimeSpan.Zero;

        while (_core.IsGameRunning())
        {
            _stopwatch.Stop();
            lag += _stopwatch.Elapsed;
            _stopwatch.Restart();

            // STEP 1: INPUT
            ProcessInput();

            // STEP 2: UPDATE
            while (lag >= _msPerUpdate)
            {
                _core.Tick(_timeStepSizeMs);
                _timeMs += _timeStepSizeMs;
                lag -= _msPerUpdate;
            }

            // STEP 3: RENDER
            var howFarIntoNextFrameMs = lag.TotalMilliseconds / _msPerUpdate.TotalMilliseconds;
            var renderWatch = Stopwatch.StartNew();
            _core.Render(howFarIntoNextFrameMs);
            renderWatch.Stop();
            Console.WriteLine($"render duration: {renderWatch.Elapsed}");
            var renderElapsed = renderWatch.Elapsed;

            // Take a break if we're ahead of time.
            var loopTimeMs = lag + renderElapsed;

            // If we spent longer than our allotted time, skip right ahead...
            if (loopTimeMs >= _msPerUpdate)
                continue;

            // ...otherwise wait until the next frame is due.
            var sleepyTime = (int)(_msPerUpdate - loopTimeMs).TotalMilliseconds;
            Console.WriteLine($"pausing game loop for {sleepyTime} ms");
            Thread.Sleep(sleepyTime);
        }
    }
}
