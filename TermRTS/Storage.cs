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

    public IEnumerable<ComponentBase> GetForEntity(int entityId);

    public IEnumerable<ComponentBase> GetForType(Type type);

    public IEnumerable<ComponentBase> GetForEntityAndType(int entityId, Type type);

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

    public IEnumerable<ComponentBase> GetForEntity(int entityId)
    {
        return _componentStores
            .Values
            .Where(v => v.ContainsKey(entityId))
            .Select(v => v[entityId])
            .SelectMany(v => v)
            .AsEnumerable();
        //.Aggregate((v1, v2) => v1.Union(v2).ToImmutableList());
    }

    public IEnumerable<ComponentBase> GetForType(Type type)
    {
        if (_cachedGetForTypeQueries.TryGetValue(type, out var cachedQuery))
            return cachedQuery;

        if (!_componentStores.TryGetValue(type, out var components))
            return Enumerable.Empty<ComponentBase>();

        var query = components
            .Values
            .SelectMany(v => v).ToCachedEnumerable();
        // ReSharper disable once PossibleMultipleEnumeration
        _cachedGetForTypeQueries.Add(type, query);
        // ReSharper disable once PossibleMultipleEnumeration
        return query;
    }

    public IEnumerable<ComponentBase> GetForEntityAndType(int entityId, Type type)
    {
        if (!_componentStores.TryGetValue(type, out var componentsByType))
            return Enumerable.Empty<ComponentBase>();

        return !componentsByType.TryGetValue(entityId, out var componentsByTypeAndEntity)
            ? Enumerable.Empty<ComponentBase>()
            : componentsByTypeAndEntity;
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