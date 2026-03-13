namespace TermRTS.Benchmark;

/// <summary>Renderer that does nothing; used for scheduler step benchmarks.</summary>
internal sealed class NoOpRenderer : IRenderer
{
    public void RenderComponents(in IReadonlyStorage storage, double timeStepSizeMs,
        double howFarIntoNextFramePercent) { }

    public void FinalizeRender() { }

    public void Shutdown() { }
}

/// <summary>System that does nothing; used for Core.Tick throughput benchmarks.</summary>
internal sealed class NoOpSystem : ISimSystem
{
    public void ProcessComponents(ulong timeStepSizeMs, in IReadonlyStorage storage) { }
}

/// <summary>System that sleeps for a fixed duration to simulate tick load.</summary>
internal sealed class BusySystem(double workTimeMs) : ISimSystem
{
    private readonly TimeSpan _workTime = TimeSpan.FromMilliseconds(workTimeMs);

    public void ProcessComponents(ulong timeStepSizeMs, in IReadonlyStorage storage)
    {
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < _workTime) { }
    }
}

/// <summary>System that calls GetAllForType and touches each component; used for Tick-with-components benchmarks.</summary>
internal sealed class GetAllAndTouchSystem : ISimSystem
{
    public void ProcessComponents(ulong timeStepSizeMs, in IReadonlyStorage storage)
    {
        var acc = 0;
        foreach (var c in storage.GetAllForType<BenchmarkComponent>())
        {
            c.Touch++;
            acc += c.EntityId;
        }
    }
}

internal sealed class NullEntity : EntityBase { }

/// <summary>Component type used by storage benchmarks; one per entity or singleton.</summary>
internal sealed class BenchmarkComponent : ComponentBase
{
    public BenchmarkComponent(int entityId) : base(entityId) { }
    public int Touch { get; set; }
}

/// <summary>Renderer that sleeps to simulate heavy render load.</summary>
internal sealed class SlowRenderer(TimeSpan renderDuration) : IRenderer
{
    public void RenderComponents(in IReadonlyStorage storage, double timeStepSizeMs,
        double howFarIntoNextFramePercent) => Thread.Sleep(renderDuration);

    public void FinalizeRender() => Thread.Sleep(renderDuration);

    public void Shutdown() { }
}
