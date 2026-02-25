using System.Text.Json.Serialization;
using log4net;
using TermRTS.Data;

namespace TermRTS;

using EntityComponents = Dictionary<int, List<ComponentBase>>;

#region IStorage Interfaces

public interface IStorage : IReadonlyStorage, IWritableStorage
{
}

public interface IReadonlyStorage
{
    IEnumerable<ComponentBase> GetAllForEntity(int entityId);

    IEnumerable<T> GetAllForType<T>();
    T? GetSingleForType<T>();

    IEnumerable<T> GetAllForTypeAndEntity<T>(int entityId);
    T? GetSingleForTypeAndEntity<T>(int entityId);

    void SwapBuffers();

    // public IEnumerable<ComponentBase> All();
}

/// <summary>
///     Interface for component storage, associating by component type and entity id.
///     It's supposed to store all components of a type in contiguous memory to allow fast access for
///     the <c>SimSystem</c>s.
/// </summary>
public interface IWritableStorage
{
    void AddComponent(ComponentBase component);

    void AddComponents(IEnumerable<ComponentBase> components);

    void RemoveComponentsByEntity(int entityId);

    void RemoveComponentsByType(Type type);

    void RemoveComponentsByEntityAndType(int entityId, Type type);
}

#endregion

#region Storage Implementation

/// <summary>
///     Storage of components by component type and entity id. Supports multiple components
///     of the same type per entity (stored in a list per (type, entity)).
/// </summary>
// TODO: Make implementation thread-safe
public class MappedCollectionStorage : IStorage
{
    #region Fields

    private static readonly ILog Log = LogManager.GetLogger(typeof(MappedCollectionStorage));

    private readonly Dictionary<Type, IEnumerable<ComponentBase>> _cachedGetForTypeQueries = new();
    private readonly Dictionary<Type, EntityComponents> _componentStores = new();

    #endregion

    #region Constructors

    public MappedCollectionStorage()
    {
    }

    [JsonConstructor]
    public MappedCollectionStorage(IList<ComponentBase> serializedComponents)
    {
        AddComponents(serializedComponents);
    }

    #endregion

    #region IReadonlyStorage Members

    public IEnumerable<ComponentBase> GetAllForEntity(int entityId)
    {
        foreach (var entityComponents in _componentStores.Values)
            if (entityComponents.TryGetValue(entityId, out var list))
                foreach (var component in list)
                    yield return component;
    }

    public IEnumerable<T> GetAllForType<T>()
    {
        if (_cachedGetForTypeQueries.TryGetValue(typeof(T), out var cachedQuery))
            return cachedQuery.Cast<T>();

        if (!_componentStores.TryGetValue(typeof(T), out var components))
            return [];

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
        var first = GetAllForType<T>().FirstOrDefault();
        if (first is not null) return first;

        Log.Error($"Cannot find component of Type {typeof(T)}");
        return default;
    }

    public IEnumerable<T> GetAllForTypeAndEntity<T>(int entityId)
    {
        if (!_componentStores.TryGetValue(typeof(T), out var componentsByType))
            return [];

        return !componentsByType.TryGetValue(entityId, out var componentsByTypeAndEntity)
            ? []
            : componentsByTypeAndEntity.Cast<T>();
    }

    public T? GetSingleForTypeAndEntity<T>(int entityId)
    {
        var first = GetAllForTypeAndEntity<T>(entityId).FirstOrDefault();
        if (first is not null) return first;

        Log.Error($"Cannot find component of Type {typeof(T)} for entity {entityId}");
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

    #endregion

    #region IWritableStorage Members

    public void AddComponent(ComponentBase component)
    {
        var type = component.GetType();
        if (!_componentStores.TryGetValue(type, out var entityComponents))
        {
            entityComponents = new EntityComponents();
            _componentStores[type] = entityComponents;
        }

        if (!entityComponents.TryGetValue(component.EntityId, out var list))
        {
            list = [];
            entityComponents[component.EntityId] = list;
        }

        list.Add(component);
        _cachedGetForTypeQueries.Remove(type);
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

    #endregion

    #region Public Members

    public void Clear()
    {
        _componentStores.Clear();
        _cachedGetForTypeQueries.Clear();
    }

    #endregion

    private IEnumerable<ComponentBase> All()
    {
        return _componentStores
            .SelectMany(store => store.Value.Values.SelectMany(l => l).AsEnumerable())
            .AsEnumerable();
    }

    #endregion

    internal List<ComponentBase> GetSerializableComponents()
    {
        return All().ToList();
    }
}