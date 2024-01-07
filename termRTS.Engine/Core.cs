using System.Diagnostics;

namespace termRTS.Engine;

// Notes to self:
// Possible optimisation - remove all new variable allocation from game loop and replace with
// assigning to private class fields.
// Ideas for double buffer implementation:
//      - https://codereview.stackexchange.com/questions/108763/simple-generic-double-buffer-pattern

/// <summary>
/// The one who ties the whole system together.
/// </summary>
/// <typeparam name="TWorld">
/// Type of the world class.
/// </typeparam>
/// <typeparam name="TComponents">
/// Type of the enum listing all component types.
/// </typeparam
internal class Core<TWorld, TComponents> where TComponents : Enum
{
    private static readonly TimeSpan MS_PER_UPDATE = TimeSpan.FromSeconds(1.0 / 60.0);

    private readonly Stopwatch _stopwatch;
    private bool _isGameRunning;
    private EventQueue<IEvent, ulong> _eventQueue;

    private TWorld _world;
    private readonly List<GameSystem<TWorld, TComponents>> _systems;
    private readonly List<GameEntity<TComponents>> _entities;
    private readonly IInput _input;
    private readonly IRenderer<TWorld, TComponents> _renderer;
    // TODO: some form of event queue for:
    //       - handling input
    //       - adding/removing entities to/from the game

    public Core(TWorld world, IInput input, IRenderer<TWorld, TComponents> renderer)
    {
        _stopwatch = new Stopwatch();
        _isGameRunning = false;
        _eventQueue = new();
        _world = world;
        _entities = new();
        _systems = new();
        _input = input;
        _renderer = renderer;
    }

    public void GameLoop()
    {
        _isGameRunning = true;
        _stopwatch.Start();
        var lag = TimeSpan.Zero;
        TimeSpan renderElapsed;

        while (_isGameRunning)
        {
            _stopwatch.Stop();
            lag += _stopwatch.Elapsed;
            _stopwatch.Restart();

            ProcessInput();

            Update(ref lag);

            Render(ref lag, out renderElapsed);

            // Take a break if we're ahead of time.
            var loopTimeMs = lag + renderElapsed;

            // If we spent longer than our allotted time, skip right ahead...
            if (loopTimeMs >= MS_PER_UPDATE)
                continue;

            // ...otherwise wait until the next frame is due.
            var sleepyTime = (int)(MS_PER_UPDATE - loopTimeMs).TotalMilliseconds;
            Console.WriteLine($"pausing game loop for {sleepyTime} ms");
            Thread.Sleep(sleepyTime);
        }
    }

    private void ProcessInput()
    {
        // TODO: Something with IInput
    }

    private void Update(ref TimeSpan lag)
    {
        // Advance as many steps as we need to catch up with lag.
        while (lag >= MS_PER_UPDATE)
        {
            // Run game logic.
            foreach (var entity in _entities)
            {
                foreach (var sys in _systems)
                {
                    sys.ProcessComponents(
                            entity.WritableComponents,
                            _entities,
                            ref _world);
                }
            }
            // TODO: Apply changes to the game world after all entities are done.

            lag -= MS_PER_UPDATE;
        }
    }

    private void Render(ref TimeSpan lag, out TimeSpan renderElapsed)
    {
        var howFarIntoNextFrameMs = lag.TotalMilliseconds / MS_PER_UPDATE.TotalMilliseconds;
        var renderWatch = Stopwatch.StartNew();

        _renderer.renderWorld(_world, howFarIntoNextFrameMs);

        foreach (var entity in _entities)
        {
            entity.ApplyChanges();
            _renderer.renderEntity(entity, howFarIntoNextFrameMs);
        }

        renderWatch.Stop();
        Console.WriteLine($"render duration: {renderWatch.Elapsed}");
        renderElapsed = renderWatch.Elapsed;
    }
}
