using System.Diagnostics;

namespace termRTS.Engine;

// Notes to self:
// Possible optimisation - remove all new variable allocation from game loop and replace with
// assigning to private class fields.
// Ideas for double buffer implementation:
//      - https://codereview.stackexchange.com/questions/108763/simple-generic-double-buffer-pattern
// Tutorial for writing a logger:
//      - https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/tutorials/interpolated-string-handler

/// <summary>
/// The one who ties the whole system together.
/// </summary>
/// <typeparam name="TWorld">
/// Type of the world class.
/// </typeparam>
/// <typeparam name="TComponents">
/// Type of the enum listing all component types.
/// </typeparam>
internal class Core<TWorld, TComponents> where TComponents : Enum
{
    private static readonly TimeSpan MS_PER_UPDATE = TimeSpan.FromSeconds(1.0 / 60.0);

    private readonly Stopwatch _stopwatch;
    private bool _isGameRunning;
    private readonly EventQueue<IEvent, ulong> _eventQueue;

    private TWorld _world;
    private readonly List<GameSystem<TWorld, TComponents>> _systems;
    private readonly List<GameEntity<TComponents>> _entities;
    private readonly List<Dictionary<TComponents, IGameComponent>> _entitiesPendingChanges;
    private readonly List<GameEntity<TComponents>> _newEntities;
    private readonly IInput _input;
    private readonly IRenderer<TWorld, TComponents> _renderer;
    // TODO: some form of event queue for:
    //       - handling input
    //       - adding/removing entities to/from the game

    public Core(TWorld world, IInput input, IRenderer<TWorld, TComponents> renderer)
    {
        _stopwatch = new Stopwatch();
        _isGameRunning = false;
        _eventQueue = new EventQueue<IEvent, ulong>();
        _world = world;
        _entities = new List<GameEntity<TComponents>>();
        _entitiesPendingChanges = new List<Dictionary<TComponents, IGameComponent>>();
        _newEntities = new List<GameEntity<TComponents>>();
        _systems = new List<GameSystem<TWorld, TComponents>>();
        _input = input;
        _renderer = renderer;
    }

    public void Shutdown()
    {
        _isGameRunning = false;
    }

    public void AddEntity(GameEntity<TComponents> entity)
    {
        _newEntities.Add(entity);
    }

    public void AddGameSystem(GameSystem<TWorld, TComponents> system)
    {
        _systems.Add(system);
    }

    public void RemoveGameSystem(GameSystem<TWorld, TComponents> system)
    {
        _systems.Remove(system);
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

            ProcessInput();

            Update(ref lag);

            Render(ref lag, out var renderElapsed);

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
            for (var i = 0; i < _entities.Count; i += 1)
            {
                foreach (var sys in _systems)
                {
                    var listView = _entities[..];
                    listView.RemoveAt(i);
                    sys.ProcessComponents(_entities[i], listView, ref _world);
                }
            }

            // Apply changes to the game world after all entities are done.
            for (var i = 0; i < _entities.Count; i += 1)
            {
                var entity = _entities[i];
                var pendingChanges = _entitiesPendingChanges[i];
                
                if (pendingChanges.Count <= 0) continue;
                
                foreach (var item in pendingChanges)
                {
                    entity.Components[item.Key] = item.Value;
                }
                pendingChanges.Clear();
            }

            _entities.RemoveAll(e => e.IsMarkedForRemoval);
            _entities.AddRange(_newEntities);
            _newEntities.Clear();

            lag -= MS_PER_UPDATE;
        }
    }

    private void Render(ref TimeSpan lag, out TimeSpan renderElapsed)
    {
        var howFarIntoNextFrameMs = lag.TotalMilliseconds / MS_PER_UPDATE.TotalMilliseconds;
        var renderWatch = Stopwatch.StartNew();

        _renderer.RenderWorld(_world, howFarIntoNextFrameMs);

        for (var i = 0; i < _entities.Count; i += 1)
        {
            _renderer.RenderEntity(_entities[i].Components, howFarIntoNextFrameMs);
        }

        renderWatch.Stop();
        Console.WriteLine($"render duration: {renderWatch.Elapsed}");
        renderElapsed = renderWatch.Elapsed;
    }
}
