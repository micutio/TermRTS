using System.Diagnostics;

namespace TermRTS;

// TODO: Add documentation
public interface ICore : IEventSink
{
    public bool IsRunning();
    public void Tick(ulong timeStepSizeMs);
    public void Render(double howFarIntoNextFrameMs, double timeStepSizeMs);
    public void Shutdown();
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
// Site for converting normal text into ASCII art:
//      - https://www.patorjk.com/software/taag/

// TODO: [ECS] Rework association of components with entities
// TODO: [SIM] Rework system iteration over entities
// TODO: [SIM] Rework application of component state changes

/// <summary>
///     The core of the engine performs the actual tick logic and is controlled by the scheduler.
/// </summary>
/// <typeparam name="TWorld">
///     Type of the world class.
/// </typeparam>
public class Core<TWorld> : ICore where TWorld : IWorld
{
    #region Constructor

    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="world"> An object representing the simulation world. </param>
    /// <param name="renderer"> An object representing the renderer. </param>
    public Core(TWorld world, IRenderer<TWorld> renderer)
    {
        _isGameRunning = true;
        _world = world;
        _renderer = renderer;
        _entities = new List<EntityBase>();
        _entitiesPendingChanges = new Dictionary<int, Dictionary<Type, IComponent>>();
        _newEntities = new List<EntityBase>();
        _systems = new List<System<TWorld>>();
    }

    #endregion

    #region IEventSink Members

    public void ProcessEvent(IEvent evt)
    {
        switch (evt.Type())
        {
            case EventType.KeyInput:
                throw new NotImplementedException();
            case EventType.MouseInput:
                throw new NotImplementedException();
            case EventType.Profile:
                throw new NotImplementedException();
            case EventType.Shutdown:
                _isGameRunning = false;
                return;
            default:
                throw new UnreachableException();
        }
    }

    #endregion

    #region Private Fields

    private bool _isGameRunning;
    private TWorld _world;
    private readonly IRenderer<TWorld> _renderer;
    private readonly List<System<TWorld>> _systems;
    private readonly List<EntityBase> _entities;
    private readonly Dictionary<int, Dictionary<Type, IComponent>> _entitiesPendingChanges;
    private readonly List<EntityBase> _newEntities;

    #endregion

    #region Public API

    /// <summary>
    ///     Prompt the simulation to stop running.
    /// </summary>
    public void Shutdown()
    {
        _renderer.Shutdown();
        Console.WriteLine("Core shut down");
    }

    /// <summary>
    ///     A method to check whether the simulation is still running.
    /// </summary>
    /// <returns>
    ///     <code>true</code> if the simulation is still running, <code>false</code> if it has
    ///     terminated.
    /// </returns>
    public bool IsRunning()
    {
        return _isGameRunning;
    }

    /// <summary>
    ///     Schedule a new entity to be added to the simulation at the beginning of the next tick.
    /// </summary>
    /// <param name="entity"> Entity object to be added. </param>
    public void AddEntity(EntityBase entity)
    {
        _newEntities.Add(entity);
    }

    /// <summary>
    ///     Schedule a range of new entities to be added to the simulation at the beginning of the
    ///     next tick.
    /// </summary>
    /// <param name="entities"> Entity objects to be added. </param>
    public void AddAllEntities(IEnumerable<EntityBase> entities)
    {
        _newEntities.AddRange(entities);
    }

    /// <summary>
    ///     Add a new system to the simulation, effective immediately.
    /// </summary>
    /// <param name="system"> System object to be added </param>
    public void AddGameSystem(System<TWorld> system)
    {
        _systems.Add(system);
    }

    /// <summary>
    ///     Remove system from the simulation, effective immediately.
    /// </summary>
    /// <param name="system"> System object to be removed </param>
    public void RemoveGameSystem(System<TWorld> system)
    {
        _systems.Remove(system);
    }

    public void Tick(ulong timeStepSizeMs)
    {
        // Two-step simulation
        // Step 1: Iterate over each system and apply it to the respective entities. The actual
        //         changes are stored separately to avoid affecting the current iteration
        // TODO: Ideally only iterate over those entities with components matching the system!
        // TODO: Create option for parallelised iteration over systems and/or entities!
        // TODO: Try flipping the `for` and `foreach` loops to see which variant is faster.
        // TODO: Idea for more efficient iteration:
        //       - rank components by count of occurrence in entities
        //       - sort entities by their highest-ranked components
        foreach (var sys in _systems)
            for (var i = 0; i < _entities.Count; i += 1)
            {
                // Create a copy of the entity list and remove this entity to not iterate over it
                // TODO: Slices are supposedly slow because they copy data. Change to better iteration strategy! 
                var thisEntity = _entities[i];
                var otherEntities = _entities.Take(i).Skip(1).Take(_entities.Count - i - 1);
                var change =
                    sys.ProcessComponents(timeStepSizeMs, thisEntity, otherEntities, ref _world);
                if (change != null) _entitiesPendingChanges[i] = change;
            }

        // Step 2: Apply changes to the game world
        for (var i = 0; i < _entities.Count; i += 1)
        {
            if (!_entitiesPendingChanges.ContainsKey(i))
                continue;

            var entity = _entities[i];
            var change = _entitiesPendingChanges[i];

            foreach (var item in change) entity.Components[item.Key] = item.Value;
        }

        _entitiesPendingChanges.Clear();

        // Clean up operations: remove 'dead' entities and add new ones
        _entities.RemoveAll(e => e.IsMarkedForRemoval);
        _entities.AddRange(_newEntities);
        _newEntities.Clear();

        // New game state:
        //  - all pending changes cleared
        //  - all pending new entities added
        //  - all to-be-removed entities removed
    }

    /// <summary>
    ///     Call the renderer to render all renderable objects.
    /// </summary>
    public void Render(double howFarIntoNextFrameMs, double timeStepSizeMs)
    {
        _renderer.RenderWorld(_world, timeStepSizeMs, howFarIntoNextFrameMs);

        for (var i = 0; i < _entities.Count; i += 1)
            _renderer.RenderEntity(_entities[i].Components, howFarIntoNextFrameMs);

        _renderer.FinalizeRender();
    }

    #endregion
}