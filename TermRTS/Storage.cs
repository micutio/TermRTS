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

    /// <summary>
    ///     Returns a read-only view of components of type T. The view is valid only until the next
    ///     write to storage (AddComponent, RemoveComponentsByEntity, etc.). Do not hold across tick
    ///     boundaries or after any mutation. Use when a system only needs to iterate once per call.
    /// </summary>
    IReadOnlyList<T> GetListForType<T>();

    T? GetSingleForType<T>();

    /// <summary>
    ///     Tries to get a single component of type <typeparamref name="T" />. Returns false and sets
    ///     <paramref name="component" /> to default when none exists; does not log. Use when "missing" is valid.
    /// </summary>
    bool TryGetSingleForType<T>(out T? component);

    IEnumerable<T> GetAllForTypeAndEntity<T>(int entityId);
    T? GetSingleForTypeAndEntity<T>(int entityId);

    /// <summary>
    ///     Tries to get a single component of type <typeparamref name="T" /> for the given entity. Returns false
    ///     and sets <paramref name="component" /> to default when none exists; does not log.
    /// </summary>
    bool TryGetSingleForTypeAndEntity<T>(int entityId, out T? component);

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
        {
            if (list.Count > 0)
                return (T?)(object)list[0];
        }

        Log.Debug($"Cannot find component of Type {typeof(T)}");
        return default;
    }

    public bool TryGetSingleForType<T>(out T? component)
    {
        component = default;
        if (!_componentStores.TryGetValue(typeof(T), out var entityComponents))
            return false;

        foreach (var list in entityComponents.Values)
        {
            if (list.Count > 0)
            {
                component = (T?)(object)list[0];
                return true;
            }
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

    #endregion

    #region Public Members

    public void Clear()
    {
        _componentStores.Clear();
        _cachedGetForTypeQueries.Clear();
        _listCache.Clear();
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

#region ContiguousStorage (Option B)

/// <summary>
///     Storage with one contiguous list per component type for cache-friendly by-type iteration.
///     By-entity and by-type-and-entity lookups scan the list. Implements <see cref="IStorage" />.
/// </summary>
public class ContiguousStorage : IStorage
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(ContiguousStorage));
    private readonly Dictionary<Type, List<ComponentBase>> _componentStores = new();

    public IEnumerable<ComponentBase> GetAllForEntity(int entityId)
    {
        foreach (var list in _componentStores.Values)
        foreach (var component in list)
            if (component.EntityId == entityId)
                yield return component;
    }

    public IEnumerable<T> GetAllForType<T>()
    {
        if (!_componentStores.TryGetValue(typeof(T), out var list))
            return [];
        return list.Cast<T>();
    }

    public IReadOnlyList<T> GetListForType<T>()
    {
        if (!_componentStores.TryGetValue(typeof(T), out var list))
            return Array.Empty<T>();
        return new ComponentListAdapter<T>(list);
    }

    /// <summary>
    ///     Returns a read-only span over components of type T (snapshot; one allocation per call).
    ///     Valid only until next write. Use for cache-friendly iteration when you have ContiguousStorage.
    /// </summary>
    public ReadOnlySpan<T> GetSpanForType<T>() where T : class
    {
        if (!_componentStores.TryGetValue(typeof(T), out var list) || list.Count == 0)
            return ReadOnlySpan<T>.Empty;
        var arr = new T[list.Count];
        for (var i = 0; i < list.Count; i++)
            arr[i] = (T)(object)list[i];
        return new ReadOnlySpan<T>(arr);
    }

    public T? GetSingleForType<T>()
    {
        if (!_componentStores.TryGetValue(typeof(T), out var list) || list.Count == 0)
        {
            Log.Debug($"Cannot find component of Type {typeof(T)}");
            return default;
        }
        return (T?)(object)list[0];
    }

    public bool TryGetSingleForType<T>(out T? component)
    {
        component = default;
        if (!_componentStores.TryGetValue(typeof(T), out var list) || list.Count == 0)
            return false;
        component = (T?)(object)list[0];
        return true;
    }

    public IEnumerable<T> GetAllForTypeAndEntity<T>(int entityId)
    {
        if (!_componentStores.TryGetValue(typeof(T), out var list))
            return [];
        return list.Where(c => c.EntityId == entityId).Cast<T>();
    }

    public T? GetSingleForTypeAndEntity<T>(int entityId)
    {
        if (!_componentStores.TryGetValue(typeof(T), out var list))
        {
            Log.Debug($"Cannot find component of Type {typeof(T)} for entity {entityId}");
            return default;
        }
        foreach (var c in list)
            if (c.EntityId == entityId)
                return (T?)(object)c;
        Log.Debug($"Cannot find component of Type {typeof(T)} for entity {entityId}");
        return default;
    }

    public bool TryGetSingleForTypeAndEntity<T>(int entityId, out T? component)
    {
        component = default;
        if (!_componentStores.TryGetValue(typeof(T), out var list))
            return false;
        foreach (var c in list)
            if (c.EntityId == entityId)
            {
                component = (T?)(object)c;
                return true;
            }
        return false;
    }

    public void SwapBuffers()
    {
        foreach (var list in _componentStores.Values)
        foreach (var component in list)
            component.SwapBuffers();
    }

    public void AddComponent(ComponentBase component)
    {
        var type = component.GetType();
        if (!_componentStores.TryGetValue(type, out var list))
        {
            list = [];
            _componentStores[type] = list;
        }
        list.Add(component);
    }

    public void AddComponents(IEnumerable<ComponentBase> components)
    {
        foreach (var component in components) AddComponent(component);
    }

    public void RemoveComponentsByEntity(int entityId)
    {
        foreach (var list in _componentStores.Values)
            list.RemoveAll(c => c.EntityId == entityId);
    }

    public void RemoveComponentsByType(Type type)
    {
        _componentStores.Remove(type);
    }

    public void RemoveComponentsByEntityAndType(int entityId, Type type)
    {
        if (!_componentStores.TryGetValue(type, out var list))
            return;
        list.RemoveAll(c => c.EntityId == entityId);
    }

    public void Clear()
    {
        _componentStores.Clear();
    }

    internal List<ComponentBase> GetSerializableComponents()
    {
        var result = new List<ComponentBase>();
        foreach (var list in _componentStores.Values)
            result.AddRange(list);
        return result;
    }
}

/// <summary>Thin read-only list view over List&lt;ComponentBase&gt; for GetListForType.</summary>
internal sealed class ComponentListAdapter<T>(List<ComponentBase> list) : IReadOnlyList<T>
{
    public int Count => list.Count;
    public T this[int index] => (T)(object)list[index];
    public IEnumerator<T> GetEnumerator() => list.Cast<T>().GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

#endregion