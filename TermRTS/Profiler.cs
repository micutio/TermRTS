namespace TermRTS;

/// <summary>
/// The profiler can sample snapshots of simulation tick and render times to compile an overview
/// of the overall engine performance.
/// </summary>
public class Profiler
{

    private readonly UInt64 _timeStepSize;

    private UInt64 _sampleSize;
    private double _avgTickTime;
    private UInt64 _minTickTime;
    private UInt64 _maxTickTime;
    private double _avgRenderTime;
    private UInt64 _minRenderTime;
    private UInt64 _maxRenderTime;
    private UInt64 _droppedFrames;

    public Profiler(UInt64 timeStepSize)
    {
        _timeStepSize = timeStepSize;
        Initialize();
    }

    private void Initialize()
    {
        _sampleSize = 0L;

        _avgTickTime = 0L;
        _minTickTime = UInt64.MaxValue;
        _maxTickTime = 0L;

        _avgRenderTime = 0L;
        _minRenderTime = UInt64.MaxValue;
        _maxRenderTime = 0L;
    }

    public void AddTickTimeSample(UInt64 tickTime, UInt64 renderTime)
    {
        // exclude invalid samples, which should be only the first ones taken
        if (tickTime == 0 || renderTime == 0)
            return;

        // refresh after every 5000 samples
        if (_sampleSize == 500)
        {
            Initialize();
        }

        _avgTickTime = (tickTime + _sampleSize * _avgTickTime) / (_sampleSize + 1);
        _minTickTime = Math.Min(_minTickTime, tickTime);
        _maxTickTime = Math.Max(_maxTickTime, tickTime);

        _avgRenderTime = (renderTime + _sampleSize * _avgRenderTime) / (_sampleSize + 1);
        _minRenderTime = Math.Min(_minRenderTime, renderTime);
        _maxRenderTime = Math.Max(_maxRenderTime, renderTime);

        if (tickTime > _timeStepSize * 2)
        {
            _droppedFrames += 1;
        }
        _sampleSize += 1;
    }

    public UInt64 SampleSize => _sampleSize;

    /// <summary>
    /// Compose a string of simulation performance information in a human-readable format.
    /// </summary>
    public override string ToString()
    {
        return $"avg tick Δt : {_avgTickTime:F1}ms [{_minTickTime},{_maxTickTime}], " +
               $"avg render Δt: {_avgRenderTime:F1}ms [{_minRenderTime}, {_maxRenderTime}], " +
               $"frames dropped: {_droppedFrames}/{_sampleSize}";
    }

}
