using BenchmarkDotNet.Attributes;
using TermRTS.Event;
using TermRTS.Storage;

namespace TermRTS.Benchmark;

#region Storage query benchmarks (plan §4)

/// <summary>
/// GetAllForType and full enumeration; N components of one type. Params: N = 100, 1K, 10K.
/// </summary>
[MemoryDiagnoser]
public class StorageGetAllForTypeBenchmark
{
    private MappedCollectionStorage _storage = null!;

    [Params(100, 1_000, 10_000)] public int ComponentCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _storage = new MappedCollectionStorage();
        for (var i = 0; i < ComponentCount; i++)
            _storage.AddComponent(new BenchmarkComponent(i));
    }

    [Benchmark(Description = "GetAllForType + enumerate (count)")]
    public int GetAllForTypeAndEnumerate()
    {
        var n = 0;
        foreach (var _ in _storage.GetAllForType<BenchmarkComponent>())
            n++;
        return n;
    }
}

/// <summary>
/// GetSingleForType and TryGetSingleForType with one component of type T (singleton path).
/// </summary>
[MemoryDiagnoser]
public class StorageGetSingleForTypeBenchmark
{
    private MappedCollectionStorage _storage = null!;

    [GlobalSetup]
    public void Setup()
    {
        _storage = new MappedCollectionStorage();
        _storage.AddComponent(new BenchmarkComponent(1));
    }

    [Benchmark(Description = "GetSingleForType (singleton)")]
    public ComponentBase? GetSingle()
    {
        return _storage.GetSingleForType<BenchmarkComponent>();
    }

    [Benchmark(Description = "TryGetSingleForType (singleton)")]
    public bool TryGetSingle()
    {
        return _storage.TryGetSingleForType<BenchmarkComponent>(out _);
    }
}

#endregion

#region Core.Tick with real workload (plan §4)

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
        _core.AddEntity(new Entity());
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

    [Params(1, 4, 8, 16)] public int SystemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _core = new Core { Renderer = new NoOpRenderer() };
        _core.AddEntity(new Entity());
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
    private readonly List<ScheduledEvent> _emittedEvents = [];

    [Params(100, 1000, 5000)] public int EntityCount { get; set; }

    [Params(4, 16)] public int SystemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _core = new Core { Renderer = new NoOpRenderer() };
        for (var i = 0; i < EntityCount; i++)
            _core.AddEntity(new Entity());
        for (var i = 0; i < SystemCount; i++)
            _core.AddSimSystem(new NoOpSystem());
    }

    [Benchmark(Description = "Core.Tick only")]
    public void Tick()
    {
        _core.Tick(16, _emittedEvents);
    }
}

/// <summary>
/// Core.Tick with M components (one per entity) and a system that GetAllForType and touches each. Params: entity count, system count.
/// </summary>
[MemoryDiagnoser]
public class CoreTickWithComponentsBenchmark
{
    private Core _core = null!;
    private readonly List<ScheduledEvent> _emittedEvents = [];

    [Params(100, 1000, 5000)] public int EntityCount { get; set; }

    [Params(1, 4)] public int SystemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _core = new Core { Renderer = new NoOpRenderer() };
        var entities = new Entity[EntityCount];
        for (var i = 0; i < EntityCount; i++)
        {
            entities[i] = new Entity();
            _core.AddEntity(entities[i]);
        }

        _core.Tick(16, _emittedEvents); // flush deferred adds
        for (var i = 0; i < EntityCount; i++)
            _core.AddComponent(new BenchmarkComponent(entities[i].Id));
        _core.Tick(16, _emittedEvents); // flush components
        for (var i = 0; i < SystemCount; i++)
            _core.AddSimSystem(new GetAllAndTouchSystem());
    }

    [Benchmark(Description = "Core.Tick with components + GetAllForType system")]
    public void Tick()
    {
        _core.Tick(16, _emittedEvents);
    }
}

/// <summary>
/// Every N ticks, mark one entity for removal and add a new one; measures Tick time and memory over many iterations.
/// </summary>
[MemoryDiagnoser]
public class CoreTickWithEntityChurnBenchmark
{
    private Core _core = null!;
    private readonly List<ScheduledEvent> _emittedEvents = [];
    private List<Entity> _entities = null!;
    private int _indexToRemove;

    [Params(50)] public int InitialEntityCount { get; set; }

    [Params(10)] public int TicksPerChurn { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _core = new Core { Renderer = new NoOpRenderer() };
        _core.AddSimSystem(new GetAllAndTouchSystem());
        _entities = new List<Entity>();
        for (var i = 0; i < InitialEntityCount; i++)
        {
            var e = new Entity();
            _entities.Add(e);
            _core.AddEntity(e);
        }

        _core.Tick(16, _emittedEvents);
        for (var i = 0; i < InitialEntityCount; i++)
            _core.AddComponent(new BenchmarkComponent(_entities[i].Id));
        _core.Tick(16, _emittedEvents);
        _indexToRemove = 0;
    }

    [Benchmark(Description = "Core.Tick with entity churn")]
    public void TickWithChurn()
    {
        for (var t = 0; t < TicksPerChurn; t++)
            _core.Tick(16, _emittedEvents);
        if (_entities.Count <= 0) return;

        var entity = _entities[_indexToRemove];
        entity.IsMarkedForRemoval = true;
        _entities[_indexToRemove] = entity;
        _core.Tick(16, _emittedEvents);
        var e = new Entity();
        _entities[_indexToRemove] = e;
        _core.AddEntity(e);
        _core.AddComponent(new BenchmarkComponent(e.Id));
        _core.Tick(16, _emittedEvents);
        _indexToRemove = (_indexToRemove + 1) % _entities.Count;
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

    [Params(2, 5, 10)] public int RenderSleepMs { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _core = new Core { Renderer = new SlowRenderer(TimeSpan.FromMilliseconds(RenderSleepMs)) };
        _core.AddEntity(new Entity());
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

#endregion