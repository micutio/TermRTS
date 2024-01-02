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
/// <typeparam name="TW">
/// Type of the world class.
/// </typeparam>
/// <typeparam name="T">
/// Type of the enum listing all component types.
/// </typeparam
internal class Core<TW, T> where T : Enum
{
    private static readonly TimeSpan MS_PER_UPDATE = TimeSpan.FromSeconds(1.0 / 60.0);

    private readonly Stopwatch _stopwatch;
    private bool _isGameRunning;

    private TW _world;
    private readonly List<GameSystem<TW, T>> _systems;
    private readonly List<Dictionary<T, IGameComponent>> _entities;
    // TODO: some form of event queue for:
    //       - handling input
    //       - adding/removing entities to/from the game

    public Core(TW world)
    {
        _stopwatch = new Stopwatch();
        _isGameRunning = false;
        _world = world;
        _entities = new List<Dictionary<T, IGameComponent>>();
        _systems = new List<GameSystem<TW, T>>();
    }

    public void GameLoop()
    {
        _isGameRunning = true;
        _stopwatch.Start();
        var lag = TimeSpan.Zero;

        while (_isGameRunning)
        {
            _stopwatch.Stop();
            lag += _stopwatch.Elapsed;
            _stopwatch.Restart();

            processInput();

            update(ref lag);

            var renderElapsed = render(lag);

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

    private void processInput()
    {

    }

    private void update(ref TimeSpan lag)
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
                            entity,
                            _entities,
                            ref _world);
                }
            }
            // TODO: Apply changes to the game world after all entities are done.

            lag -= MS_PER_UPDATE;
        }
    }

    private TimeSpan render(TimeSpan lag)
    {
        var howFarIntoNextFrameMs = lag.TotalMilliseconds / MS_PER_UPDATE.TotalMilliseconds;
        var renderWatch = Stopwatch.StartNew();

        // TODO: Render World and all Entity objects

        renderWatch.Stop();
        Console.WriteLine($"render duration: {renderWatch.Elapsed}");
        return renderWatch.Elapsed;
    }
}
