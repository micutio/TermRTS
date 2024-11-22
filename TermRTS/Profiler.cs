namespace TermRTS;

/// <summary>
///     The profiler can sample snapshots of simulation tick and render times to compile an overview
///     of the overall engine performance.
/// </summary>
public class Profiler
{
    private readonly ulong _timeStepSize;
    
    private double _lastLoopTime;
    private double _lastTickTime;
    private double _lastRenderTime;
    private double _lastFps;
    
    private ulong _droppedFrames;
    
    private double _maxLoopTime;
    private double _maxTickTime;
    private double _maxRenderTime;
    private double _maxFps;
    private double _minLoopTime;
    private double _minTickTime;
    private double _minRenderTime;
    private double _minFps;
    
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
        
        _lastFps = 1000d / loopTimeMs;
        _minFps = Math.Min(_minFps, _lastFps);
        _maxFps = Math.Max(_maxFps, _lastFps);
        
        SampleSize += 1;
    }
    
    /// <summary>
    ///     Compose a string of simulation performance information in a human-readable format.
    /// </summary>
    public override string ToString()
    {
        return $"Loop {_minLoopTime:F1}, {_lastLoopTime:F1}, {_maxLoopTime:F1} | " +
               $"Tick {_minTickTime:F1}, {_lastTickTime:F1}, {_maxTickTime:F1} | " +
               $"Render {_minRenderTime:F1}, {_lastRenderTime:F1}, {_maxRenderTime:F1} | " +
               $"FPS {_minFps:F1}, {_lastFps:F1}, {_maxFps:F1} | " +
               $"Frames dropped {_droppedFrames}";
    }
    
    private void Initialize()
    {
        SampleSize = 0L;
        
        _lastLoopTime = 0.0;
        _minLoopTime = ulong.MaxValue;
        _maxLoopTime = 0L;
        
        _lastTickTime = 0.0;
        _minTickTime = ulong.MaxValue;
        _maxTickTime = 0L;
        
        _lastRenderTime = 0.0;
        _minRenderTime = ulong.MaxValue;
        _maxRenderTime = 0L;
        
        _lastFps = 0.0;
        _minFps = ulong.MaxValue;
        _maxFps = 0L;
    }
}