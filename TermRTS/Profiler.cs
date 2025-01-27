namespace TermRTS;

/// <summary>
///     The profiler can sample snapshots of simulation tick and render times to compile an overview
///     of the overall engine performance.
/// </summary>
public class Profiler
{
    private readonly ulong _timeStepSize;

    private ulong _droppedFrames;
    private ulong _lastFps;

    private ulong _lastLoopTime;
    private ulong _lastRenderTime;
    private ulong _lastTickTime;
    private ulong _maxFps;

    private ulong _maxLoopTime;
    private ulong _maxRenderTime;
    private ulong _maxTickTime;
    private ulong _minFps;
    private ulong _minLoopTime;
    private ulong _minRenderTime;
    private ulong _minTickTime;

    public Profiler(ulong timeStepSize)
    {
        _timeStepSize = timeStepSize;
        Initialize();
    }

    public ulong SampleSize { get; private set; }


    public void AddTickTimeSample(ulong loopTimeMs, ulong tickTimeMs, ulong renderTimeMs)
    {
        // Exclude invalid samples, which should be only the first ones taken.
        if (loopTimeMs == 0L) // || tickTimeMs == 0 || renderTimeMs == 0)
            return;

        // Refresh after every 500 samples.
        //if (SampleSize == 500L) Initialize();

        _lastLoopTime = loopTimeMs;
        _minLoopTime = Math.Min(_minLoopTime, loopTimeMs);
        _maxLoopTime = Math.Max(_maxLoopTime, loopTimeMs);

        _lastTickTime = tickTimeMs;
        _minTickTime = Math.Min(_minTickTime, tickTimeMs);
        _maxTickTime = Math.Max(_maxTickTime, tickTimeMs);

        _lastRenderTime = renderTimeMs;
        _minRenderTime = Math.Min(_minRenderTime, renderTimeMs);
        _maxRenderTime = Math.Max(_maxRenderTime, renderTimeMs);

        if (tickTimeMs + renderTimeMs > _timeStepSize) _droppedFrames += 1;
        // _droppedFrames += Math.Max(0, Convert.ToUInt64(loopTimeMs) - 1 / _timeStepSize);

        _lastFps = 1000 / loopTimeMs;
        _minFps = Math.Min(_minFps, _lastFps);
        _maxFps = Math.Max(_maxFps, _lastFps);

        SampleSize += 1;
    }

    /// <summary>
    ///     Compose a string of simulation performance information in a human-readable format.
    /// </summary>
    public override string ToString()
    {
        /*
        return $"Loop {_minLoopTime:D3}, {_lastLoopTime:D3}, {_maxLoopTime:D3} | " +
               $"Tick {_minTickTime:D3}, {_lastTickTime:D3}, {_maxTickTime:D3} | " +
               $"Render {_minRenderTime:D3}, {_lastRenderTime:D3}, {_maxRenderTime:D3} | " +
               $"FPS {_minFps:D3}, {_lastFps:D3}, {_maxFps:D3} | " +
               $"Frames dropped {_droppedFrames}";
               */
        return $"Loop {_lastLoopTime:D3} " +
               $"Tick {_lastTickTime:D3} " +
               $"Render {_lastRenderTime:D3} " +
               $"FPS {_lastFps:D3} " +
               $"Frames dropped {_droppedFrames}";
    }

    private void Initialize()
    {
        SampleSize = 0L;

        _lastLoopTime = 0L;
        _minLoopTime = ulong.MaxValue;
        _maxLoopTime = 0L;

        _lastTickTime = 0L;
        _minTickTime = ulong.MaxValue;
        _maxTickTime = 0L;

        _lastRenderTime = 0L;
        _minRenderTime = ulong.MaxValue;
        _maxRenderTime = 0L;

        _lastFps = 0L;
        _minFps = ulong.MaxValue;
        _maxFps = 0L;
    }
}