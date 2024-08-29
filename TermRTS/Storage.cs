using System.Collections.Immutable;

namespace TermRTS;

using EntityComponents = Dictionary<int, IList<IComponent>>;

// TODO: Handle efficient generating and applying of component changes.
// TODO: ID generation
// TODO: Make query results read-only and changes write-only

#region IStorage Interface

/// <summary>
/// Interface for component storage, associating by component type and entity id.
/// It's supposed to store all components of a type in contiguous memory to allow fast access for
/// the <c>SimSystem</c>s.
/// </summary>
public interface IStorage
{
    public void AddComponent(IComponent component);

    public void AddComponents(IComponent[] components);

    public void RemoveComponents(int entityId, Type type);

    public void RemoveComponents(int entityId);

    public void RemoveComponents(Type type);

    public IEnumerable<IComponent> GetForEntity(int entityId);

    public IEnumerable<IComponent> GetForType(Type type);

    public IEnumerable<IComponent> GetForEntityAndType(int entityId, Type type);
}

#endregion

#region Storage Implementation

/// <summary>
/// Storage of components, associating by component type and entity id.
/// NOTE: Only supports one component per type per ID!
/// </summary>
public class MappedCollectionStorage : IStorage
{
    // private Dictionary<Type, IComponentStore> componentStores;
    private readonly Dictionary<Type, EntityComponents> _componentStores = new();

    public void AddComponent(IComponent component)
    {
        if (!_componentStores.ContainsKey(component.GetType()))
            _componentStores.Add(component.GetType(), new EntityComponents());

        _componentStores[component.GetType()][component.EntityId].Add(component);
        // componentsDict.Add(component.EntityId, component);
    }

    public void AddComponents(IComponent[] components)
    {
        foreach (var component in components) AddComponent(component);
    }

    public void RemoveComponents(int entityId, Type type)
    {
        _componentStores[type][entityId].Clear();
    }

    public void RemoveComponents(int entityId)
    {
        foreach (var componentTypeDict in _componentStores
                     .Values
                     .Where(componentTypeDict => componentTypeDict.ContainsKey(entityId)))
            componentTypeDict[entityId].Clear();
    }

    public void RemoveComponents(Type type)
    {
        _componentStores.Remove(type);
    }

    public IEnumerable<IComponent> GetForEntity(int entityId)
    {
        return _componentStores
            .Values
            .Where(v => v.ContainsKey(entityId))
            .Select(v => v[entityId])
            .Aggregate((v1, v2) => v1.Union(v2).ToImmutableList());
    }

    public IEnumerable<IComponent> GetForType(Type type)
    {
        return _componentStores[type]
            .Values
            .Aggregate((v1, v2) => v1.Union(v2).ToImmutableList());
    }

    public IEnumerable<IComponent> GetForEntityAndType(int entityId, Type type)
    {
        return _componentStores[type][entityId];
    }
}

#endregion