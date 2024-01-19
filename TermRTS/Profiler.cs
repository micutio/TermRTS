namespace TermRTS;

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

    public override string ToString()
    {
        return $"Average Tick timespan: {_avgTickTime}ms ({_minTickTime}, {_maxTickTime}) of which {_avgRenderTime}ms [{_minRenderTime}, {_maxRenderTime}] was spent rendering, dropped frames: {_droppedFrames}/{_sampleSize}";
    }

}
