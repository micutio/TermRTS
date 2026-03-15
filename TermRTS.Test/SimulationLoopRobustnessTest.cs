using TermRTS.Event;

namespace TermRTS.Test;

/// <summary>
/// Unit tests for simulation loop robustness: time advancement under tick/render load,
/// lag catch-up over real time, event ordering, and stress with many entities/systems.
/// </summary>
public class SimulationLoopRobustnessTest
{
    private static readonly TimeSpan DefaultRunTimeout = TimeSpan.FromSeconds(10);

    [Theory]
    [ClassData(typeof(TestCoreParallelAndStorageConfigs))]
    public void Shutdown_under_heavy_tick_load_completes_and_time_advances(Core core)
    {
        const int tickCount = 12;
        const ulong timeStepSizeMs = 16;
        var scheduler = new Scheduler(core);
        core.AddSimSystem(new TerminatorSystem(scheduler.EventQueue, tickCount));
        core.AddSimSystem(new BusySystem(2.0));
        core.AddSimSystem(new BusySystem(1.0));
        core.AddSimSystem(new BusySystem(3.0));
        core.AddSimSystem(new BusySystem(4.0));
        core.AddSimSystem(new BusySystem(0.5));
        core.AddEntity(new NullEntity());

        var simulation = new Simulation(scheduler);
        Shared.RunWithTimeout(simulation, DefaultRunTimeout);

        Assert.Equal(tickCount * timeStepSizeMs, scheduler.TimeMs);
        Assert.False(scheduler.IsActive);
    }

    [Theory]
    [ClassData(typeof(TestCoreParallelAndStorageConfigs))]
    public void Heavy_render_load_does_not_prevent_tick_progression(Core core)
    {
        const int tickCount = 8;
        const ulong timeStepSizeMs = 16;
        core.Renderer = new SlowRenderer(TimeSpan.FromMilliseconds(6));
        var scheduler = new Scheduler(core);
        core.AddSimSystem(new TerminatorSystem(scheduler.EventQueue, tickCount));
        core.AddEntity(new NullEntity());

        var simulation = new Simulation(scheduler);
        Shared.RunWithTimeout(simulation, DefaultRunTimeout);

        // Shutdown is processed at next step start; one extra tick can happen in the same step.
        Assert.True(scheduler.TimeMs >= tickCount * timeStepSizeMs);
        Assert.True(scheduler.TimeMs <= (tickCount + 1) * timeStepSizeMs);
        Assert.False(scheduler.IsActive);
    }

    [Theory]
    [ClassData(typeof(TestCoreParallelAndStorageConfigs))]
    public void Time_advances_over_real_time(Core core)
    {
        const int tickCount = 20;
        var scheduler = new Scheduler(core);
        core.AddSimSystem(new TerminatorSystem(scheduler.EventQueue, tickCount));
        core.AddEntity(new NullEntity());

        var simulation = new Simulation(scheduler);
        Shared.RunWithTimeout(simulation, DefaultRunTimeout);

        Assert.True(scheduler.TimeMs >= (ulong)tickCount * 16);
        Assert.False(scheduler.IsActive);
    }

    [Theory]
    [ClassData(typeof(TestCoreParallelAndStorageConfigs))]
    public void Delayed_shutdown_with_load_respects_event_ordering_and_completes(Core core)
    {
        const ulong shutdownAtMs = 12 * 16;
        var scheduler = new Scheduler(core);
        scheduler.EventQueue.EnqueueEvent(ScheduledEvent.From(new Shutdown(), shutdownAtMs));
        core.AddSimSystem(new BusySystem(1.0));
        core.AddSimSystem(new BusySystem(2.0));
        core.AddEntity(new NullEntity());

        var simulation = new Simulation(scheduler);
        Shared.RunWithTimeout(simulation, DefaultRunTimeout);

        Assert.Equal(shutdownAtMs, scheduler.TimeMs);
        Assert.False(scheduler.IsActive);
    }

    [Theory]
    [ClassData(typeof(TestCoreParallelAndStorageConfigs))]
    public void Many_entities_and_systems_stress_no_hang(Core core)
    {
        const int tickCount = 50;
        const int entityCount = 500;
        const int systemCount = 10;
        var scheduler = new Scheduler(core);
        core.AddSimSystem(new TerminatorSystem(scheduler.EventQueue, tickCount));
        for (var i = 0; i < systemCount; i++)
            core.AddSimSystem(new BusySystem(0.05));
        for (var i = 0; i < entityCount; i++)
            core.AddEntity(new NullEntity());

        var simulation = new Simulation(scheduler);
        Shared.RunWithTimeout(simulation, DefaultRunTimeout);

        Assert.Equal((ulong)tickCount * 16, scheduler.TimeMs);
        Assert.False(scheduler.IsActive);
    }
}