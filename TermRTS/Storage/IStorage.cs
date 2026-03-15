namespace TermRTS.Storage;

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

    List<ComponentBase> GetSerializableComponents();
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

    void Clear();
}

#endregion