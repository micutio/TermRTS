using System.Diagnostics;

namespace TermRTS;

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

/// <summary>
///     The core of the engine performs the actual tick logic and is controlled by the scheduler.
/// </summary>
public class Core : IEventSink
{
    #region Constructor
    
    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="renderer"> An object representing the renderer. </param>
    /// <param name="isParallelized">
    ///     Whether the game systems should be processed in parallel.
    /// </param>
    public Core(IRenderer renderer, bool isParallelized = false)
    {
        _isGameRunning = true;
        _renderer = renderer;
        _entities = new List<EntityBase>();
        _components = new MappedCollectionStorage();
        _newEntities = new List<EntityBase>();
        _newComponents = new List<ComponentBase>();
        _systems = new List<ISimSystem>();
        
        _isParallelized = isParallelized;
    }
    
    #endregion
    
    #region IEventSink Members
    
    /// <inheritdoc />
    public void ProcessEvent(IEvent evt)
    {
        switch (evt.Type())
        {
            case EventType.KeyInput:
                break;
            case EventType.MouseInput:
                break;
            case EventType.Profile:
                break;
            case EventType.Shutdown:
                _isGameRunning = false;
                return;
            default:
                throw new UnreachableException();
        }
    }
    
    #endregion
    
    #region Private Fields
    
    private readonly IRenderer _renderer;
    private readonly List<ISimSystem> _systems;
    private readonly List<EntityBase> _entities;
    private readonly MappedCollectionStorage _components;
    private readonly List<EntityBase> _newEntities;
    private readonly List<ComponentBase> _newComponents;
    
    private readonly bool _isParallelized;
    
    private bool _isGameRunning;
    
    #endregion
    
    #region ICore Members
    
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
    ///     Prepares to spawn new entities. This always happens at the end of a simulation tick.
    /// </summary>
    public void SpawnNewEntities()
    {
        if (_newEntities.Count != 0)
        {
            _entities.AddRange(_newEntities);
            _newEntities.Clear();
        }
        
        if (_newComponents.Count != 0)
        {
            foreach (var c in _newComponents) _components.AddComponent(c);
            _newComponents.Clear();
        }
    }
    
    /// <summary>
    ///     Performs one simulation tick.
    /// </summary>
    /// <param name="timeStepSizeMs">
    ///     Indicates how much time is being simulated within this one tick.
    /// </param>
    public void Tick(ulong timeStepSizeMs)
    {
        // Two-step simulation
        // Step 1: Iterate over each system and apply it to the respective entities.
        if (_isParallelized)
            // Is it possible to set the thread count for parallel processing?
            foreach (var sys in _systems.AsParallel())
                sys.ProcessComponents(timeStepSizeMs, _components);
        else
            foreach (var sys in _systems)
                sys.ProcessComponents(timeStepSizeMs, _components);
        
        _components.SwapBuffers();
        
        // Clean up operations: remove 'dead' entities and add new ones
        // var entityIdsToRemove = _entities.Where(e => e.IsMarkedForRemoval).Select(e => e.Id);
        // foreach (var id in entityIdsToRemove) _components.RemoveComponentsByEntity(id);
        // _entities.RemoveAll(e => e.IsMarkedForRemoval);
        
        var i = 0;
        // TODO: Verify that modification of the list while iterating over it is not causing issues!
        while (i < _entities.Count)
        {
            if (!_entities[i].IsMarkedForRemoval)
            {
                i++;
                continue;
            }
            
            _components.RemoveComponentsByEntity(_entities[i].Id);
            _entities.RemoveAt(i);
        }
        
        SpawnNewEntities();
        
        // New game state should look like this:
        //  - all pending changes cleared
        //  - all pending new entities added
        //  - all to-be-removed entities removed
    }
    
    /// <summary>
    ///     Call the renderer to render all renderable objects.
    /// </summary>
    public void Render(double timeStepSizeMs, double howFarIntoNextFramePercent)
    {
        _renderer.RenderComponents(_components, timeStepSizeMs, howFarIntoNextFramePercent);
        _renderer.FinalizeRender();
    }
    
    /// <summary>
    ///     Prompt the simulation to stop running.
    /// </summary>
    public void Shutdown()
    {
        _renderer.Shutdown();
    }
    
    #endregion
    
    #region Public Members
    
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
    ///     Schedule a new component to be added to the simulation at the beginning of the next tick.
    /// </summary>
    /// <param name="component"> Component object to be added. </param>
    public void AddComponent(ComponentBase component)
    {
        _newComponents.Add(component);
    }
    
    /// <summary>
    ///     Schedule a range of new components to be added to the simulation at the beginning of the
    ///     next tick.
    /// </summary>
    /// <param name="components"> Entity objects to be added. </param>
    public void AddAllComponents(IEnumerable<ComponentBase> components)
    {
        _newComponents.AddRange(components);
    }
    
    /// <summary>
    ///     Add a new system to the simulation, effective immediately.
    /// </summary>
    /// <param name="system"> System object to be added </param>
    public void AddSimSystem(ISimSystem system)
    {
        _systems.Add(system);
    }
    
    /// <summary>
    ///     Remove system from the simulation, effective immediately.
    /// </summary>
    /// <param name="system"> System object to be removed </param>
    public void RemoveSimSystem(ISimSystem system)
    {
        _systems.Remove(system);
    }
    
    #endregion
}