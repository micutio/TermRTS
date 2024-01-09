namespace termRTS.Engine;

public interface ICore
{
    public bool IsGameRunning();
    public void ProcessInput();
    public void Tick();
    public void Render(double howFarIntoNextFrameMs);
}

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
public class Core<TWorld, TComponents> : ICore where TComponents : Enum
{
    private bool _isGameRunning;
    private readonly EventQueue<IEvent, ulong> _eventQueue;

    private TWorld _world;
    private readonly List<GameSystem<TWorld, TComponents>> _systems;
    private readonly List<GameEntity<TComponents>> _entities;
    private readonly Dictionary<int, Dictionary<TComponents, IGameComponent>> _entitiesPendingChanges;
    private readonly List<GameEntity<TComponents>> _newEntities;
    private readonly IInput _input;
    private readonly IRenderer<TWorld, TComponents> _renderer;

    public Core(TWorld world, IInput input, IRenderer<TWorld, TComponents> renderer)
    {
        _isGameRunning = false;
        _eventQueue = new EventQueue<IEvent, ulong>();
        _world = world;
        _entities = new List<GameEntity<TComponents>>();
        _entitiesPendingChanges = new Dictionary<int, Dictionary<TComponents, IGameComponent>>();
        _newEntities = new List<GameEntity<TComponents>>();
        _systems = new List<GameSystem<TWorld, TComponents>>();
        _input = input;
        _renderer = renderer;
    }

    public void Shutdown()
    {
        _isGameRunning = false;
    }

    public bool IsGameRunning()
    {
        return _isGameRunning;
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

    public void ProcessInput()
    {
    }

    public void Tick()
    {
        // Run game logic.
        // NOTE: Try flipping the `for` and `foreach` loops to see which variant is faster.
        for (var i = 0; i < _entities.Count; i += 1)
        {
            foreach (var sys in _systems)
            {
                var listView = _entities[..];
                listView.RemoveAt(i);
                var change = sys.ProcessComponents(_entities[i], listView, ref _world);
                if (change != null)
                {
                    _entitiesPendingChanges[i] = change;
                }
            }
        }

        // Apply changes to the game world after all entities are done.
        for (var i = 0; i < _entities.Count; i += 1)
        {
            if (!_entitiesPendingChanges.ContainsKey(i))
                continue;

            var entity = _entities[i];
            var change = _entitiesPendingChanges[i];

            foreach (var item in change)
            {
                entity.Components[item.Key] = item.Value;
            }
        }
        _entitiesPendingChanges.Clear();

        _entities.RemoveAll(e => e.IsMarkedForRemoval);
        _entities.AddRange(_newEntities);
        _newEntities.Clear();

        // New game state:
        //  - all pending changes cleared
        //  - all pending new entities added
        //  - all to-be-removed entities removed

    }

    public void Render(double howFarIntoNextFrameMs)
    {
        _renderer.RenderWorld(_world, howFarIntoNextFrameMs);

        for (var i = 0; i < _entities.Count; i += 1)
        {
            _renderer.RenderEntity(_entities[i].Components, howFarIntoNextFrameMs);
        }
    }
}
