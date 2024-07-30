namespace TermRTS;

/// <summary>
///     The profiler can sample snapshots of simulation tick and render times to compile an overview
///     of the overall engine performance.
/// </summary>
public class Profiler
{
    private readonly ulong _timeStepSize;
    private double _avgRenderTime;
    private double _avgTickTime;
    private ulong _droppedFrames;
    private ulong _maxRenderTime;
    private ulong _maxTickTime;
    private ulong _minRenderTime;
    private ulong _minTickTime;

    public Profiler(ulong timeStepSize)
    {
        _timeStepSize = timeStepSize;
        Initialize();
    }

    public ulong SampleSize { get; private set; }

    private void Initialize()
    {
        SampleSize = 0L;

        _avgTickTime = 0L;
        _minTickTime = ulong.MaxValue;
        _maxTickTime = 0L;

        _avgRenderTime = 0L;
        _minRenderTime = ulong.MaxValue;
        _maxRenderTime = 0L;
    }

    public void AddTickTimeSample(ulong tickTime, ulong renderTime)
    {
        // exclude invalid samples, which should be only the first ones taken
        if (tickTime == 0 || renderTime == 0)
            return;

        // refresh after every 5000 samples
        if (SampleSize == 500) Initialize();

        _avgTickTime = (tickTime + SampleSize * _avgTickTime) / (SampleSize + 1);
        _minTickTime = Math.Min(_minTickTime, tickTime);
        _maxTickTime = Math.Max(_maxTickTime, tickTime);

        _avgRenderTime = (renderTime + SampleSize * _avgRenderTime) / (SampleSize + 1);
        _minRenderTime = Math.Min(_minRenderTime, renderTime);
        _maxRenderTime = Math.Max(_maxRenderTime, renderTime);

        if (tickTime > _timeStepSize * 2) _droppedFrames += 1;
        SampleSize += 1;
    }

    /// <summary>
    ///     Compose a string of simulation performance information in a human-readable format.
    /// </summary>
    public override string ToString()
    {
        return $"avg tick Δt : {_avgTickTime:F1}ms [{_minTickTime},{_maxTickTime}], " +
               $"avg render Δt: {_avgRenderTime:F1}ms [{_minRenderTime}, {_maxRenderTime}], " +
               $"frames dropped: {_droppedFrames}/{SampleSize}";
    }
}