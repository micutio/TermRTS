using System.Text.Json.Serialization;
using log4net;
using TermRTS.Data;

namespace TermRTS.Storage;

using EntityComponents = Dictionary<int, List<ComponentBase>>;

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
    private readonly Dictionary<Type, object> _listCache = new();
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

    public IReadOnlyList<T> GetListForType<T>()
    {
        var type = typeof(T);
        if (_listCache.TryGetValue(type, out var cached) && cached is IReadOnlyList<T> listView)
            return listView;

        if (!_componentStores.TryGetValue(type, out var components))
        {
            var empty = new List<T>(0);
            _listCache[type] = empty;
            return empty;
        }

        var result = new List<T>();
        foreach (var list in components.Values)
        foreach (var c in list)
            result.Add((T)(object)c);
        _listCache[type] = result;
        return result;
    }

    public T? GetSingleForType<T>()
    {
        if (!_componentStores.TryGetValue(typeof(T), out var entityComponents))
        {
            Log.Debug($"Cannot find component of Type {typeof(T)}");
            return default;
        }

        foreach (var list in entityComponents.Values)
            if (list.Count > 0)
                return (T?)(object)list[0];

        Log.Debug($"Cannot find component of Type {typeof(T)}");
        return default;
    }

    public bool TryGetSingleForType<T>(out T? component)
    {
        component = default;
        if (!_componentStores.TryGetValue(typeof(T), out var entityComponents))
            return false;

        foreach (var list in entityComponents.Values)
            if (list.Count > 0)
            {
                component = (T?)(object)list[0];
                return true;
            }

        return false;
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
        if (!_componentStores.TryGetValue(typeof(T), out var entityComponents)
            || !entityComponents.TryGetValue(entityId, out var list)
            || list.Count == 0)
        {
            Log.Debug($"Cannot find component of Type {typeof(T)} for entity {entityId}");
            return default;
        }

        return (T?)(object)list[0];
    }

    public bool TryGetSingleForTypeAndEntity<T>(int entityId, out T? component)
    {
        component = default;
        if (!_componentStores.TryGetValue(typeof(T), out var entityComponents)
            || !entityComponents.TryGetValue(entityId, out var list)
            || list.Count == 0)
            return false;

        component = (T?)(object)list[0];
        return true;
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

    public List<ComponentBase> GetSerializableComponents()
    {
        return All().ToList();
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
        _listCache.Remove(type);
    }

    public void AddComponents(IEnumerable<ComponentBase> components)
    {
        foreach (var component in components) AddComponent(component);
    }

    public void RemoveComponentsByEntity(int entityId)
    {
        var affectedTypes = new List<Type>();
        foreach (var (type, entityComponents) in _componentStores)
        {
            if (!entityComponents.TryGetValue(entityId, out var list)) continue;
            list.Clear();
            entityComponents.Remove(entityId);
            affectedTypes.Add(type);
        }

        foreach (var type in affectedTypes)
        {
            _cachedGetForTypeQueries.Remove(type);
            _listCache.Remove(type);
        }
    }

    public void RemoveComponentsByType(Type type)
    {
        _componentStores.Remove(type);
        _cachedGetForTypeQueries.Remove(type);
        _listCache.Remove(type);
    }

    public void RemoveComponentsByEntityAndType(int entityId, Type type)
    {
        if (!_componentStores.TryGetValue(type, out var componentsByType))
            return;

        if (!componentsByType.TryGetValue(entityId, out var componentsByTypeAndEntity))
            return;

        componentsByTypeAndEntity.Clear();
        _cachedGetForTypeQueries.Remove(type);
        _listCache.Remove(type);
    }

    public void Clear()
    {
        _componentStores.Clear();
        _cachedGetForTypeQueries.Clear();
        _listCache.Clear();
    }

    #endregion

    #region Private Methods

    private IEnumerable<ComponentBase> All()
    {
        return _componentStores
            .SelectMany(store => store.Value.Values.SelectMany(l => l).AsEnumerable())
            .AsEnumerable();
    }

    #endregion
}