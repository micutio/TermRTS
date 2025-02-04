using log4net;
using TermRTS.Data;

namespace TermRTS;

using EntityComponents = Dictionary<int, List<ComponentBase>>;

#region IStorage Interface

/// <summary>
///     Interface for component storage, associating by component type and entity id.
///     It's supposed to store all components of a type in contiguous memory to allow fast access for
///     the <c>SimSystem</c>s.
/// </summary>
public interface IStorage
{
    public void AddComponent(ComponentBase component);
    
    public void AddComponents(IEnumerable<ComponentBase> components);
    
    public void RemoveComponentsByEntity(int entityId);
    
    public void RemoveComponentsByType(Type type);
    
    public void RemoveComponentsByEntityAndType(int entityId, Type type);
    
    public IEnumerable<ComponentBase> GetAllForEntity(int entityId);
    
    public IEnumerable<T> GetAllForType<T>();
    public T? GetSingleForType<T>();
    
    public IEnumerable<T> GetAllForTypeAndEntity<T>(int entityId);
    public T? GetSingleForTypeAndEntity<T>(int entityId);
    
    public void SwapBuffers();
    
    // public IEnumerable<ComponentBase> All();
}

#endregion

#region Storage Implementation

/// <summary>
///     Storage of components, associating by component type and entity id.
///     NOTE: Only supports one component per type per ID!
/// </summary>
// TODO: Make implementation thread-safe
public class MappedCollectionStorage : IStorage
{
    private readonly Dictionary<Type, IEnumerable<ComponentBase>> _cachedGetForTypeQueries = new();
    
    // private Dictionary<Type, ComponentBaseStore> componentStores;
    private readonly Dictionary<Type, EntityComponents> _componentStores = new();
    
    private readonly ILog _log;
    
    public MappedCollectionStorage()
    {
        _log = LogManager.GetLogger(GetType());
    }
    
    public void AddComponent(ComponentBase component)
    {
        if (!_componentStores.ContainsKey(component.GetType()))
            _componentStores.Add(component.GetType(), new EntityComponents());
        
        var entityComponents = _componentStores[component.GetType()];
        if (!entityComponents.ContainsKey(component.EntityId))
            entityComponents.Add(component.EntityId, []);
        
        _componentStores[component.GetType()][component.EntityId].Add(component);
        
        _cachedGetForTypeQueries.Remove(component.GetType());
        // componentsDict.Add(component.EntityId, component);
    }
    
    public void AddComponents(IEnumerable<ComponentBase> components)
    {
        foreach (var component in components) AddComponent(component);
    }
    
    public void RemoveComponentsByEntity(int entityId)
    {
        foreach (var componentTypeDict in _componentStores
                     .Values
                     .Where(componentTypeDict => componentTypeDict.ContainsKey(entityId)))
            componentTypeDict[entityId].Clear();
        _cachedGetForTypeQueries.Clear();
    }
    
    public void RemoveComponentsByType(Type type)
    {
        _componentStores.Remove(type);
        _cachedGetForTypeQueries.Remove(type);
    }
    
    public void RemoveComponentsByEntityAndType(int entityId, Type type)
    {
        if (!_componentStores.TryGetValue(type, out var componentsByType))
            return;
        
        if (!componentsByType.TryGetValue(entityId, out var componentsByTypeAndEntity))
            return;
        
        componentsByTypeAndEntity.Clear();
        _cachedGetForTypeQueries.Remove(type);
    }
    
    public IEnumerable<ComponentBase> GetAllForEntity(int entityId)
    {
        return _componentStores
            .Values
            .Where(v => v.ContainsKey(entityId))
            .Select(v => v[entityId])
            .SelectMany(v => v)
            .AsEnumerable();
    }
    
    public IEnumerable<T> GetAllForType<T>()
    {
        if (_cachedGetForTypeQueries.TryGetValue(typeof(T), out var cachedQuery)) return cachedQuery.Cast<T>();
        
        if (!_componentStores.TryGetValue(typeof(T), out var components)) return Enumerable.Empty<T>();
        
        var query = components
            .Values
            .SelectMany(v => v);
        // ReSharper disable once PossibleMultipleEnumeration
        _cachedGetForTypeQueries.Add(typeof(T), query.ToCachedEnumerable());
        // ReSharper disable once PossibleMultipleEnumeration
        return query.Cast<T>();
    }
    
    public T? GetSingleForType<T>()
    {
        try
        {
            if (GetAllForType<T>().First() is { } c) return c;
        }
        catch (InvalidOperationException e)
        {
            _log.Error($"Cannot find component of Type {typeof(T)}:\n{e}");
        }
        
        return default;
    }
    
    public IEnumerable<T> GetAllForTypeAndEntity<T>(int entityId)
    {
        if (!_componentStores.TryGetValue(typeof(T), out var componentsByType)) return Enumerable.Empty<T>();
        
        return !componentsByType.TryGetValue(entityId, out var componentsByTypeAndEntity)
            ? Enumerable.Empty<T>()
            : componentsByTypeAndEntity.Cast<T>();
    }
    
    public T? GetSingleForTypeAndEntity<T>(int entityId)
    {
        try
        {
            if (GetAllForTypeAndEntity<T>(entityId).First() is { } c) return c;
        }
        catch (InvalidOperationException e)
        {
            _log.Error($"Cannot find component of Type {typeof(T)}:\n{e}");
        }
        
        return default;
    }
    
    public void SwapBuffers()
    {
        foreach (var componentByEntity in _componentStores.Values)
        foreach (var componentList in componentByEntity.Values)
        foreach (var component in componentList)
            component.SwapBuffers();
        
        // Alternative iteration strategies:
        
        // foreach (var component in All()) component.SwapBuffers();
        
        /*
        foreach (var component in
                 from componentByEntity in _componentStores.Values
                 from componentList in componentByEntity.Values
                 from component in componentList
                 select component)
        */
    }
    
    private IEnumerable<ComponentBase> All()
    {
        return _componentStores
            .SelectMany(store => store.Value.Values.SelectMany(l => l).AsEnumerable())
            .AsEnumerable();
    }
}

#endregion