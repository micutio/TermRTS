namespace termRTS.Engine;

/// <summary>
/// Game entity, providing facilities for registering components.
/// </summary>
/// <typeparam name="TComponents">
/// Type of the Enum listing all component types.
/// </typeparam>
public abstract class GameEntity<TComponents> where TComponents : Enum
{
    public bool IsMarkedForRemoval { get; set; } = false;

    public Dictionary<TComponents, IGameComponent> Components { get; } = new();

    protected void AddComponent(TComponents systemType, IGameComponent component)
    {
        Components.Add(systemType, component);
    }

    public void SetComponent(TComponents systemType, IGameComponent component)
    {
        Components[systemType] = component;
    }

    public bool IsCompatibleWith(TComponents[] componentTypes)
    {
        return componentTypes.All(Components.ContainsKey);
    }
}

