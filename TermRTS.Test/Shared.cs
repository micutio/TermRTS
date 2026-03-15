using TermRTS.Event;
using TermRTS.Storage;

namespace TermRTS.Test;

/// <summary>
/// Shared test helpers and reusable types (renderers, systems, entities, sinks).
/// </summary>
public static class Shared
{
    /// <summary>
    /// Runs <paramref name="simulation"/>.Run() in a background task and waits up to <paramref name="timeout"/>.
    /// Throws <see cref="TimeoutException"/> if the run does not complete in time (e.g. loop stuck).
    /// </summary>
    public static void RunWithTimeout(Simulation simulation, TimeSpan timeout)
    {
        var task = Task.Run(() => simulation.Run());
        if (!task.Wait(timeout))
            throw new TimeoutException(
                $"Simulation.Run() did not complete within {timeout.TotalSeconds} s.");
    }
}

public class NullRenderer : IRenderer
{
    #region IRenderer Members

    public void RenderComponents(in IReadonlyStorage storage, double timeStepSizeMs,
        double howFarIntoNextFramePercent)
    {
    }

    public void Shutdown()
    {
    }

    void IRenderer.FinalizeRender()
    {
    }

    #endregion
}

/// <summary>
/// Renderer that sleeps in both render methods to simulate heavy render load.
/// Used to verify that a slow render does not block tick progression (ticks run before render).
/// </summary>
public class SlowRenderer(TimeSpan renderDuration) : IRenderer
{
    public void RenderComponents(in IReadonlyStorage storage, double timeStepSizeMs,
        double howFarIntoNextFramePercent)
    {
        Thread.Sleep(renderDuration);
    }

    public void Shutdown()
    {
    }

    void IRenderer.FinalizeRender()
    {
        Thread.Sleep(renderDuration);
    }
}

public class TestCoreParallelAndStorageConfigs : TheoryData<Core>
{
    public TestCoreParallelAndStorageConfigs()
    {
        Add(
            new Core(new MappedCollectionStorage())
            {
                Renderer = new NullRenderer(),
                IsParallelized = true
            });
        Add(
            new Core(new MappedCollectionStorage())
            {
                Renderer = new NullRenderer(),
                IsParallelized = false
            });
        Add(
            new Core(new ContiguousStorage())
            {
                Renderer = new NullRenderer(),
                IsParallelized = true
            });
        Add(
            new Core(new ContiguousStorage())
            {
                Renderer = new NullRenderer(),
                IsParallelized = false
            });
    }
}

public class IStorageImplementations : TheoryData<IStorage>
{
    public IStorageImplementations()
    {
        Add(new MappedCollectionStorage());
        Add(new ContiguousStorage());
    }
}

public class NullEntity : EntityBase
{
}

public class TerminatorSystem(SchedulerEventQueue queue, int remainingTicks) : ISimSystem
{
    private int _remainingTicks = remainingTicks;

    #region ISimSystem Members

    public void ProcessComponents(ulong timeStepSizeMs, in IReadonlyStorage storage)
    {
        Interlocked.Decrement(ref _remainingTicks);

        if (_remainingTicks != 0) return;

        queue.EnqueueEvent(ScheduledEvent.From(new Shutdown()));
    }

    #endregion
}

/// <summary>
/// A system that wastes a certain amount of time to simulate work.
/// </summary>
public class BusySystem(double workTimeMs) : ISimSystem
{
    private readonly TimeSpan _workTime = TimeSpan.FromMilliseconds(workTimeMs);

    public void ProcessComponents(ulong timeStepSizeMs, in IReadonlyStorage storage)
    {
        var start = DateTime.Now;
        while (DateTime.Now - start < _workTime) ;
    }
}

/// <summary>
/// Sink that records all events it receives for assertion in tests.
/// </summary>
internal sealed class RecordingSink : IEventSink
{
    private readonly List<IEvent> _received = [];

    public IReadOnlyList<IEvent> Received => _received;

    public void ProcessEvent(IEvent evt)
    {
        _received.Add(evt);
    }
}