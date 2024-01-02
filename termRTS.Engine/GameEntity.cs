namespace termRTS.Engine;

/// <summary>
/// Game entity, providing facilities for registering components.
/// </summary>
/// <typeparam name="T">
/// Type of the Enum listing all component types.
/// </typeparam>
public abstract class GameEntity<T> where T : Enum
{
    private readonly Dictionary<T, IGameComponent> _writableComponents = new();
    private readonly Dictionary<T, IGameComponent> _readableComponents = new();

    protected void AddComponent(T systemType, IGameComponent component)
    {
        _writableComponents.Add(systemType, component);
        _readableComponents.Add(systemType, (IGameComponent)component.Clone());
    }

    public void SetComponent(T systemType, IGameComponent component)
    {
        _readableComponents[systemType] = component;
    }

    public IGameComponent GetWritableComponent(T componentType) => _writableComponents[componentType];

    public IGameComponent GetReadonlyComponent(T componentType) => _readableComponents[componentType];

    public bool IsCompatibleWith(T[] componentTypes)
    {
        return componentTypes.All(_writableComponents.ContainsKey);
    }

    public void ApplyChanges()
    {
        foreach (var item in _writableComponents)
        {
            _readableComponents[item.Key] = (IGameComponent)item.Value.Clone();
        }
    }
}

