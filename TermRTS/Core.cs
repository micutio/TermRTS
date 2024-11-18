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

/// <summary>
///     The core of the engine performs the actual tick logic and is controlled by the scheduler.
/// </summary>
public class Core : ICore
{
    #region Constructor
    
    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="renderer"> An object representing the renderer. </param>
    public Core(IRenderer renderer)
    {
        _isGameRunning = true;
        _renderer = renderer;
        _entities = new List<EntityBase>();
        _components = new MappedCollectionStorage();
        _newEntities = new List<EntityBase>();
        _newComponents = new List<ComponentBase>();
        _systems = new List<SimSystem>();
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
    private readonly IRenderer _renderer;
    private readonly List<SimSystem> _systems;
    private readonly List<EntityBase> _entities;
    private readonly MappedCollectionStorage _components;
    private readonly List<EntityBase> _newEntities;
    private readonly List<ComponentBase> _newComponents;
    
    #endregion
    
    #region Public API
    
    /// <summary>
    ///     Prompt the simulation to stop running.
    /// </summary>
    public void Shutdown()
    {
        _renderer.Shutdown();
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
    public void AddSimSystem(SimSystem system)
    {
        _systems.Add(system);
    }
    
    /// <summary>
    ///     Remove system from the simulation, effective immediately.
    /// </summary>
    /// <param name="system"> System object to be removed </param>
    public void RemoveSimSystem(SimSystem system)
    {
        _systems.Remove(system);
    }
    
    public void Tick(ulong timeStepSizeMs)
    {
        // Two-step simulation
        // Step 1: Iterate over each system and apply it to the respective entities. The actual
        //         changes are stored separately to avoid affecting the current iteration
        
        // TODO: Make parallelised iteration over systems and/or entities configurable:
        //      - on/off
        //      - thread count
        foreach (var sys in _systems.AsParallel())
            sys.ProcessComponents(timeStepSizeMs, _components);
        
        _components.SwapBuffers();
        
        // Clean up operations: remove 'dead' entities and add new ones
        var entityIdsToRemove = _entities.Where(e => e.IsMarkedForRemoval).Select(e => e.Id);
        foreach (var id in entityIdsToRemove) _components.RemoveComponentsByEntity(id);
        _entities.RemoveAll(e => e.IsMarkedForRemoval);
        
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
        
        // New game state should look like this:
        //  - all pending changes cleared
        //  - all pending new entities added
        //  - all to-be-removed entities removed
    }
    
    /// <summary>
    ///     Call the renderer to render all renderable objects.
    /// </summary>
    public void Render(double timeStepSizeMs, double howFarIntoNextFrameMs)
    {
        _renderer.RenderComponents(_components, timeStepSizeMs, howFarIntoNextFrameMs);
        
        _renderer.FinalizeRender();
    }
    
    #endregion
}