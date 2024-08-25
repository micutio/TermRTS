namespace TermRTS;

// TODO: Handle efficient generating and applying of component changes.
// TODO: ID generation

/// <summary>
/// Interface for component storage, associating by component type and entity id.
/// It's supposed to store all components of a type in contiguous memory to allow fast access for
/// the <c>SimSystem</c>s.
/// </summary>
public interface IStorage
{
    public void AddComponent(IComponent component);

    public void AddComponents(IComponent[] components);

    public void RemoveComponent(int entityId, Type type);

    public void RemoveComponents(int entityId);

    public void RemoveComponents(Type type);

    public ArraySegment<IComponent> GetForEntity(int entityId);

    public ArraySegment<IComponent> GetForType(Type type);

    public ArraySegment<IComponent> GetForEntityAndType(int entityId, Type type);
}

/// <summary>
/// Storage of components, associating by component type and entity id.
/// </summary>
public class Storage
{

}