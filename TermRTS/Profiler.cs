namespace TermRTS;

/// <summary>
///     The profiler can sample snapshots of simulation tick and render times to compile an overview
///     of the overall engine performance.
/// </summary>
public class Profiler
{
    private readonly ulong _timeStepSize;

    private double _avgLoopTime;
    private double _avgTickTime;
    private double _avgRenderTime;
    private double _avgFps;
    private double _lastFps;

    private ulong _droppedFrames;

    private ulong _maxLoopTime;
    private ulong _maxTickTime;
    private ulong _maxRenderTime;
    private ulong _maxFps;
    private ulong _minLoopTime;
    private ulong _minTickTime;
    private ulong _minRenderTime;
    private ulong _minFps;

    public Profiler(ulong timeStepSize)
    {
        _timeStepSize = timeStepSize;
        Initialize();
    }

    public ulong SampleSize { get; private set; }


    public void AddTickTimeSample(double loopTimeMs, double tickTimeMs, double renderTimeMs)
    {
        // exclude invalid samples, which should be only the first ones taken
        if (loopTimeMs == 0) // || tickTimeMs == 0 || renderTimeMs == 0)
            return;

        // refresh after every 500 samples
        //if (SampleSize == 500) Initialize();

        _avgLoopTime = (loopTimeMs + SampleSize * _avgLoopTime) / (SampleSize + 1);
        _minLoopTime = Convert.ToUInt64(Math.Min(_minLoopTime, loopTimeMs));
        _maxLoopTime = Convert.ToUInt64(Math.Max(_maxLoopTime, loopTimeMs));

        _avgTickTime = (tickTimeMs + SampleSize * _avgTickTime) / (SampleSize + 1);
        _minTickTime = Convert.ToUInt64(Math.Min(_minTickTime, tickTimeMs));
        _maxTickTime = Convert.ToUInt64(Math.Max(_maxTickTime, tickTimeMs));

        _avgRenderTime = (renderTimeMs + SampleSize * _avgRenderTime) / (SampleSize + 1);
        _minRenderTime = Convert.ToUInt64(Math.Min(_minRenderTime, renderTimeMs));
        _maxRenderTime = Convert.ToUInt64(Math.Max(_maxRenderTime, renderTimeMs));

        // if (tickTime > _timeStepSize * 2) _droppedFrames += 1;
        _droppedFrames += Convert.ToUInt64(loopTimeMs) / _timeStepSize;

        _lastFps = 1000d / loopTimeMs;
        _avgFps = (_lastFps + SampleSize * _avgFps) / (SampleSize + 1);
        _minFps = Convert.ToUInt64(Math.Min(_minFps, _lastFps));
        _maxFps = Convert.ToUInt64(Math.Max(_maxFps, _lastFps));

        SampleSize += 1;
    }

    /// <summary>
    ///     Compose a string of simulation performance information in a human-readable format.
    /// </summary>
    public override string ToString()
    {
        return $"Loop {_minLoopTime:D2}, {_avgLoopTime:F1}, {_maxLoopTime:D2} | " +
               $"Tick {_minTickTime:D2}, {_avgTickTime:F1}, {_maxTickTime:D2} | " +
               $"Render {_minRenderTime:D2}, {_avgRenderTime:F1}, {_maxRenderTime:D2} | " +
               $"FPS {_minFps:D2}, {_lastFps:F1}, {_maxFps:D2} | " +
               $"Frames dropped {_droppedFrames}";
    }

    private void Initialize()
    {
        SampleSize = 0L;

        _avgLoopTime = 0.0;
        _minLoopTime = ulong.MaxValue;
        _maxLoopTime = 0L;

        _avgTickTime = 0.0;
        _minTickTime = ulong.MaxValue;
        _maxTickTime = 0L;

        _avgRenderTime = 0.0;
        _minRenderTime = ulong.MaxValue;
        _maxRenderTime = 0L;

        _avgFps = 0.0;
        _minFps = ulong.MaxValue;
        _maxFps = 0L;
    }
}