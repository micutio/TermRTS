using System.Text;
using log4net;

namespace TermRTS.Storage;

/// <summary>
///     Storage with one contiguous list per component type for cache-friendly by-type iteration.
///     By-entity and by-type-and-entity lookups scan the list. Implements <see cref="IStorage" />.
/// </summary>
public class ContiguousStorage : IStorage
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(ContiguousStorage));
    private readonly Dictionary<Type, List<ComponentBase>> _componentStores = new();
    private readonly Dictionary<Type, Dictionary<int, List<int>>> _entityIndices = new();

    #region IReadonlyStorage Members

    public IEnumerable<ComponentBase> GetAllForEntity(int entityId)
    {
        foreach (var (type, indicesByEntity) in _entityIndices)
            if (indicesByEntity.TryGetValue(entityId, out var indices) &&
                _componentStores.TryGetValue(type, out var list))
                foreach (var index in indices)
                    yield return list[index];
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
            Log.Debug(new StringBuilder().Append("Cannot find component of Type ")
                .Append(typeof(T))
                .ToString());
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
        var type = typeof(T);
        if (!_entityIndices.TryGetValue(type, out var indicesByEntity) ||
            !indicesByEntity.TryGetValue(entityId, out var indices))
            return [];
        if (!_componentStores.TryGetValue(type, out var list))
            return [];
        return indices.Select(i => (T)(object)list[i]);
    }

    public T? GetSingleForTypeAndEntity<T>(int entityId)
    {
        var type = typeof(T);
        if (!_entityIndices.TryGetValue(type, out var indicesByEntity) ||
            !indicesByEntity.TryGetValue(entityId, out var indices) || indices.Count == 0)
        {
            Log.Debug(new StringBuilder().Append("Cannot find component of Type ")
                .Append(typeof(T))
                .Append(" for entity ")
                .Append(entityId)
                .ToString());
            return default;
        }

        if (!_componentStores.TryGetValue(type, out var list))
        {
            Log.Debug(new StringBuilder().Append("Cannot find component of Type ")
                .Append(typeof(T))
                .Append(" for entity ")
                .Append(entityId)
                .ToString());
            return default;
        }

        return (T?)(object)list[indices[0]];
    }

    public bool TryGetSingleForTypeAndEntity<T>(int entityId, out T? component)
    {
        component = default;
        var type = typeof(T);
        if (!_entityIndices.TryGetValue(type, out var indicesByEntity) ||
            !indicesByEntity.TryGetValue(entityId, out var indices) || indices.Count == 0)
            return false;
        if (!_componentStores.TryGetValue(type, out var list))
            return false;
        component = (T?)(object)list[indices[0]];
        return true;
    }

    public void SwapBuffers()
    {
        foreach (var list in _componentStores.Values)
        foreach (var component in list)
            component.SwapBuffers();
    }

    public List<ComponentBase> GetSerializableComponents()
    {
        var result = new List<ComponentBase>();
        foreach (var list in _componentStores.Values)
            result.AddRange(list);
        return result;
    }

    #endregion

    #region IWritableStorage Members

    public void AddComponent(ComponentBase component)
    {
        var type = component.GetType();
        if (!_componentStores.TryGetValue(type, out var list))
        {
            list = [];
            _componentStores[type] = list;
        }

        list.Add(component);

        // Update entity indices
        if (!_entityIndices.TryGetValue(type, out var indicesByEntity))
        {
            indicesByEntity = new Dictionary<int, List<int>>();
            _entityIndices[type] = indicesByEntity;
        }

        if (!indicesByEntity.TryGetValue(component.EntityId, out var indices))
        {
            indices = [];
            indicesByEntity[component.EntityId] = indices;
        }

        indices.Add(list.Count - 1);
    }

    public void AddComponents(IEnumerable<ComponentBase> components)
    {
        foreach (var component in components) AddComponent(component);
    }

    public void RemoveComponentsByEntity(int entityId)
    {
        foreach (var (type, indicesByEntity) in _entityIndices)
        {
            if (!indicesByEntity.TryGetValue(entityId, out var indicesToRemove) ||
                !_componentStores.TryGetValue(type, out var list))
                continue;

            // Sort indices descending to remove from end first
            indicesToRemove.Sort((a, b) => b.CompareTo(a));

            foreach (var index in indicesToRemove)
            {
                list.RemoveAt(index);
                // Update indices for other entities
                foreach (var (otherEntityId, otherIndices) in indicesByEntity)
                {
                    if (otherEntityId == entityId) continue;
                    for (var i = 0; i < otherIndices.Count; i++)
                        if (otherIndices[i] > index)
                            otherIndices[i]--;
                }
            }

            indicesByEntity.Remove(entityId);
        }
    }

    public void RemoveComponentsByType(Type type)
    {
        _componentStores.Remove(type);
        _entityIndices.Remove(type);
    }

    public void RemoveComponentsByEntityAndType(int entityId, Type type)
    {
        if (!_entityIndices.TryGetValue(type, out var indicesByEntity) ||
            !indicesByEntity.TryGetValue(entityId, out var indicesToRemove))
            return;
        if (!_componentStores.TryGetValue(type, out var list))
            return;

        // Sort indices descending
        indicesToRemove.Sort((a, b) => b.CompareTo(a));

        foreach (var index in indicesToRemove)
        {
            list.RemoveAt(index);
            // Update indices for other entities
            foreach (var (otherEntityId, otherIndices) in indicesByEntity)
            {
                if (otherEntityId == entityId) continue;
                for (var i = 0; i < otherIndices.Count; i++)
                    if (otherIndices[i] > index)
                        otherIndices[i]--;
            }
        }

        indicesByEntity.Remove(entityId);
    }

    public void Clear()
    {
        _componentStores.Clear();
        _entityIndices.Clear();
    }

    #endregion
}

/// <summary>Thin read-only list view over List&lt;ComponentBase&gt; for GetListForType.</summary>
internal sealed class ComponentListAdapter<T>(List<ComponentBase> list) : IReadOnlyList<T>
{
    public int Count => list.Count;
    public T this[int index] => (T)(object)list[index];

    public IEnumerator<T> GetEnumerator()
    {
        return list.Cast<T>().GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}