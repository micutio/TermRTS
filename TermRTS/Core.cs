using System.Diagnostics;

namespace TermRTS;

public interface ICore : IEventSink
{
    public bool IsGameRunning();
    public void Tick(UInt128 timeStepSizeMs);
    public void Render(double howFarIntoNextFrameMs);
}

// Notes to self:
// Possible optimisation - remove all new variable allocation from game loop and replace with
// assigning to private class fields.
// Ideas for double buffer implementation:
//      - https://codereview.stackexchange.com/questions/108763/simple-generic-double-buffer-pattern
// Tutorial for writing a logger:
//      - https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/tutorials/interpolated-string-handler
// Tutorial for async channels & multiplexers:
//      - https://deniskyashif.com/2019/12/08/csharp-channels-part-1/

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
    private TWorld _world;
    private readonly IRenderer<TWorld, TComponents> _renderer;
    private readonly List<System<TWorld, TComponents>> _systems;
    private readonly List<EntityBase<TComponents>> _entities;
    private readonly Dictionary<int, Dictionary<TComponents, IComponent>> _entitiesPendingChanges;
    private readonly List<EntityBase<TComponents>> _newEntities;

    public Core(TWorld world, IRenderer<TWorld, TComponents> renderer)
    {
        _isGameRunning = true;
        _world = world;
        _renderer = renderer;
        _entities = new List<EntityBase<TComponents>>();
        _entitiesPendingChanges = new Dictionary<int, Dictionary<TComponents, IComponent>>();
        _newEntities = new List<EntityBase<TComponents>>();
        _systems = new List<System<TWorld, TComponents>>();
    }

    public void Shutdown()
    {
        _isGameRunning = false;
    }

    public bool IsGameRunning()
    {
        return _isGameRunning;
    }

    public void AddEntity(EntityBase<TComponents> entity)
    {
        _newEntities.Add(entity);
    }

    public void AddGameSystem(System<TWorld, TComponents> system)
    {
        _systems.Add(system);
    }

    public void RemoveGameSystem(System<TWorld, TComponents> system)
    {
        _systems.Remove(system);
    }

    public void ProcessEvent(IEvent evt)
    {
        switch (evt.getType())
        {
            case EventType.KeyInput:
                throw new NotImplementedException();
            case EventType.MouseInput:
                throw new NotImplementedException();
            case EventType.Output:
                throw new NotImplementedException();
            case EventType.Shutdown:
                _isGameRunning = false;
                return;
            default:
                throw new UnreachableException();
        }
    }

    public void Tick(UInt128 timeStepSizeMs)
    {
        // Run game logic.
        // NOTE: Try flipping the `for` and `foreach` loops to see which variant is faster.
        for (var i = 0; i < _entities.Count; i += 1)
        {
            foreach (var sys in _systems)
            {
                var listView = _entities[..];
                listView.RemoveAt(i);
                var change = sys.ProcessComponents(timeStepSizeMs, _entities[i], listView, ref _world);
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
