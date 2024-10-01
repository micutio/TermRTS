using System.Collections.Immutable;

namespace TermRTS;

using EntityComponents = Dictionary<int, IList<ComponentBase>>;

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
    public void AddComponent(ComponentBase component);

    public void AddComponents(ComponentBase[] components);

    public void RemoveComponents(int entityId, Type type);

    public void RemoveComponents(int entityId);

    public void RemoveComponents(Type type);

    public IEnumerable<ComponentBase> GetForEntity(int entityId);

    public IEnumerable<ComponentBase> GetForType(Type type);

    public IEnumerable<ComponentBase> GetForEntityAndType(int entityId, Type type);
    
    //public IEnumerable<ComponentBase> All();
    public void SwapBuffers();
}

#endregion

#region Storage Implementation

/// <summary>
/// Storage of components, associating by component type and entity id.
/// NOTE: Only supports one component per type per ID!
/// </summary>
public class MappedCollectionStorage : IStorage
{
    // private Dictionary<Type, ComponentBaseStore> componentStores;
    private readonly Dictionary<Type, EntityComponents> _componentStores = new();

    public void AddComponent(ComponentBase component)
    {
        if (!_componentStores.ContainsKey(component.GetType()))
            _componentStores.Add(component.GetType(), new EntityComponents());

        _componentStores[component.GetType()][component.EntityId].Add(component);
        // componentsDict.Add(component.EntityId, component);
    }

    public void AddComponents(ComponentBase[] components)
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

    public IEnumerable<ComponentBase> GetForEntity(int entityId)
    {
        return _componentStores
            .Values
            .Where(v => v.ContainsKey(entityId))
            .Select(v => v[entityId])
            .SelectMany(v => v);
        //.Aggregate((v1, v2) => v1.Union(v2).ToImmutableList());
    }

    public IEnumerable<ComponentBase> GetForType(Type type)
    {
        return _componentStores[type]
            .Values
            .SelectMany(v => v);
        //.Aggregate((v1, v2) => v1.Union(v2).ToImmutableList());
    }

    public IEnumerable<ComponentBase> GetForEntityAndType(int entityId, Type type)
    {
        return _componentStores[type][entityId];
    }
    
    
    public void SwapBuffers()
    {
        // foreach (var component in _componentStores.SelectMany(store => store.Value.Values.SelectMany(l => l)))
        foreach (var component in All())
        {
            component.SwapBuffers();
        }
    }
    
    private IEnumerable<ComponentBase> All()
    {
        return _componentStores
            .SelectMany(store => store.Value.Values.SelectMany(l => l));
    }
}

#endregion