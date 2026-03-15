using TermRTS.Event;

namespace TermRTS.Test;

public class EngineTest
{
    [Theory]
    [ClassData(typeof(TestCoreParallelAndStorageConfigs))]
    public void TestCoreShutdown(Core core)
    {
        Assert.True(core.IsRunning());
        core.Tick(16L);
        core.Tick(16L);
        core.Tick(16L);
        core.Tick(16L);
        core.ProcessEvent(new Event<Shutdown>());
        Assert.False(core.IsRunning());
    }

    [Theory]
    [ClassData(typeof(TestCoreParallelAndStorageConfigs))]
    public void TestSchedulerShutdown(Core core)
    {
        // Setup Scheduler
        var scheduler = new Scheduler(core);
        var watcherSystem = new TerminatorSystem(scheduler.EventQueue, 12);

        core.AddSimSystem(watcherSystem);
        core.AddSimSystem(new BusySystem(2.0d));
        core.AddSimSystem(new BusySystem(1.0d));
        core.AddSimSystem(new BusySystem(3.0d));
        core.AddSimSystem(new BusySystem(4.0d));

        core.AddEntity(new NullEntity());

        // Run it (with timeout so a stuck loop fails instead of hanging CI)
        var simulation = new Simulation(scheduler);
        Shared.RunWithTimeout(simulation, TimeSpan.FromSeconds(10));

        // It should terminate after 12 ticks of 16ms simulated time each.
        const ulong finalTime = 12 * 16;
        Assert.Equal(finalTime, scheduler.TimeMs);
    }

    [Theory]
    [ClassData(typeof(TestCoreParallelAndStorageConfigs))]
    public void TestBusyCore(Core core)
    {
        // Setup Scheduler
        var scheduler = new Scheduler(core);
        var watcherSystem = new TerminatorSystem(scheduler.EventQueue, 12);

        core.AddSimSystem(watcherSystem);
        core.AddSimSystem(new BusySystem(2.0d));
        core.AddSimSystem(new BusySystem(1.0d));
        core.AddSimSystem(new BusySystem(3.0d));
        core.AddSimSystem(new BusySystem(4.0d));

        core.AddEntity(new NullEntity());

        // Run it (with timeout so a stuck loop fails instead of hanging CI)
        var simulation = new Simulation(scheduler);
        Shared.RunWithTimeout(simulation, TimeSpan.FromSeconds(10));

        // It should terminate after 12 ticks of 16ms simulated time each.
        const ulong finalTime = 12 * 16;
        Assert.Equal(finalTime, scheduler.TimeMs);
    }

    [Theory]
    [ClassData(typeof(TestCoreParallelAndStorageConfigs))]
    public void TestDelayedEventScheduling(Core core)
    {
        // Set up Scheduler
        var scheduler = new Scheduler(core);
        scheduler.EventQueue.EnqueueEvent(ScheduledEvent.From(new Shutdown(), 12 * 16));

        // Run it (with timeout so a stuck loop fails instead of hanging CI)
        var simulation = new Simulation(scheduler);
        Shared.RunWithTimeout(simulation, TimeSpan.FromSeconds(10));

        // It should terminate after 12 ticks of 16ms simulated time each.
        const ulong finalTime = 12 * 16;
        Assert.Equal(finalTime, scheduler.TimeMs);
    }

    [Theory]
    [ClassData(typeof(TestCoreParallelAndStorageConfigs))]
    public void TestSerialization(Core core)
    {
        // Setup Scheduler
        var scheduler = new Scheduler(core);
        var watcherSystem = new TerminatorSystem(scheduler.EventQueue, 12);
        scheduler.AddEventSink(core, typeof(Shutdown));
        core.AddSimSystem(watcherSystem);
        core.AddEntity(new NullEntity());
        // Setup Simulation and Persistence
        var persistence = new Persistence();
        var serializationSuccess =
            persistence.PutSimStateToJson(ref scheduler, out var expectedJson, out _);

        Assert.True(serializationSuccess);
        Assert.NotNull(expectedJson);

        var deserializationSuccess =
            persistence.GetSimStateFromJson(ref scheduler, expectedJson, out _);

        Assert.True(deserializationSuccess);

        var serializationSuccess2 =
            persistence.PutSimStateToJson(ref scheduler, out var actualJson, out _);

        Assert.True(serializationSuccess2);
        Assert.NotNull(actualJson);
        Assert.Equal(expectedJson, actualJson);
    }
}