namespace TermRTS;

/// <summary>
/// Game entity, providing facilities for registering components.
/// </summary>
/// <typeparam name="TComponents">
/// Type of the Enum listing all component types.
/// </typeparam>
public abstract class EntityBase<TComponents> where TComponents : Enum
{
    public bool IsMarkedForRemoval { get; set; } = false;

    public Dictionary<TComponents, IComponent> Components { get; } = new();

    protected void AddComponent(TComponents systemType, IComponent component)
    {
        Components.Add(systemType, component);
    }

    public void SetComponent(TComponents systemType, IComponent component)
    {
        Components[systemType] = component;
    }

    public bool IsCompatibleWith(TComponents[] componentTypes)
    {
        return componentTypes.All(Components.ContainsKey);
    }
}

