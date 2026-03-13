using BenchmarkDotNet.Attributes;

namespace TermRTS.Benchmark;

/// <summary>
/// Baseline: scheduler step overhead with no systems and NullRenderer.
/// </summary>
[MemoryDiagnoser]
public class SchedulerStepBaselineBenchmark
{
    private Scheduler _scheduler = null!;
    private Core _core = null!;

    [GlobalSetup]
    public void Setup()
    {
        _core = new Core { Renderer = new NoOpRenderer() };
        _core.AddEntity(new NullEntity());
        _scheduler = new Scheduler(_core, 16.0, 16);
        _scheduler.Prepare();
    }

    [Benchmark(Description = "Scheduler step (no load)")]
    public void SimulationStep()
    {
        if (_scheduler.IsActive)
            _scheduler.SimulationStep();
    }
}

/// <summary>
/// Scheduler step with K BusySystems (fixed small work per system) to see how step time grows with system count.
/// </summary>
[MemoryDiagnoser]
public class SchedulerStepTickLoadBenchmark
{
    private Scheduler _scheduler = null!;
    private Core _core = null!;

    [Params(1, 4, 8, 16)]
    public int SystemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _core = new Core { Renderer = new NoOpRenderer() };
        _core.AddEntity(new NullEntity());
        for (var i = 0; i < SystemCount; i++)
            _core.AddSimSystem(new BusySystem(0.1));
        _scheduler = new Scheduler(_core, 16.0, 16);
        _scheduler.Prepare();
    }

    [Benchmark(Description = "Scheduler step (tick load)")]
    public void SimulationStep()
    {
        if (_scheduler.IsActive)
            _scheduler.SimulationStep();
    }
}

/// <summary>
/// Core.Tick only (no scheduler/render); measures ECS tick throughput with N entities and M systems.
/// </summary>
[MemoryDiagnoser]
public class CoreTickOnlyBenchmark
{
    private Core _core = null!;

    [Params(100, 1000, 5000)]
    public int EntityCount { get; set; }

    [Params(4, 16)]
    public int SystemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _core = new Core { Renderer = new NoOpRenderer() };
        for (var i = 0; i < EntityCount; i++)
            _core.AddEntity(new NullEntity());
        for (var i = 0; i < SystemCount; i++)
            _core.AddSimSystem(new NoOpSystem());
    }

    [Benchmark(Description = "Core.Tick only")]
    public void Tick()
    {
        _core.Tick(16);
    }
}

/// <summary>
/// Optional: step time when render dominates (renderer does fixed work per frame).
/// </summary>
[MemoryDiagnoser]
public class SchedulerStepHeavyRenderBenchmark
{
    private Scheduler _scheduler = null!;
    private Core _core = null!;

    [Params(2, 5, 10)]
    public int RenderSleepMs { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _core = new Core { Renderer = new SlowRenderer(TimeSpan.FromMilliseconds(RenderSleepMs)) };
        _core.AddEntity(new NullEntity());
        _scheduler = new Scheduler(_core, 16.0, 16);
        _scheduler.Prepare();
    }

    [Benchmark(Description = "Scheduler step (heavy render)")]
    public void SimulationStep()
    {
        if (_scheduler.IsActive)
            _scheduler.SimulationStep();
    }
}
