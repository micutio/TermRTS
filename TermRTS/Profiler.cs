namespace TermRTS;

public struct Profiler
{

    private UInt64 _sampleSize;
    private UInt64 _avgTickTime;
    private UInt64 _minTickTime;
    private UInt64 _maxTickTime;

    public Profiler()
    {
        _sampleSize = 0L;
        _avgTickTime = 0L;
        _minTickTime = 0L;
        _maxTickTime = 0L;
    }

    public void addTickTimeSample(UInt64 tickTime)
    {
        _avgTickTime = (tickTime + _sampleSize * _avgTickTime) / (_sampleSize + 1);
        _sampleSize += 1;
        _minTickTime = Math.Min(_minTickTime, tickTime);
    }

}
