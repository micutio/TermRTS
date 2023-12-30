namespace termRTS.Engine;

/// <summary>
/// Game entity, providing facilities for registering components.
/// </summary>
/// <typeparam name="T">
/// Type of the Enum listing all component types.
/// </typeparam>
public abstract class GameEntity<T> where T : Enum
{
    private readonly Dictionary<T, IGameComponent> _writeableComponents = new Dictionary<T, IGameComponent>();
    private readonly Dictionary<T, IGameComponent> _readableComponents = new Dictionary<T, IGameComponent>();

    protected void AddComponent(T systemType, IGameComponent component)
    {
        _writeableComponents.Add(systemType, component);
        _readableComponents.Add(systemType, (IGameComponent)component.Clone());
    }

    public void SetComponent(T systemType, IGameComponent component)
    {
        _readableComponents[systemType] = component;
    }

    public IGameComponent getWriteableComponent(T componentType) => _writeableComponents[componentType];

    public IGameComponent getReadonlyComponent(T componentType) => _readableComponents[componentType];

    public bool isCompatibleWith(T[] componentTypes)
    {
        return componentTypes.All(_writeableComponents.ContainsKey);
    }

    public void applyChanges()
    {
        foreach (KeyValuePair<T, IGameComponent> item in _writeableComponents)
        {
            _readableComponents[item.Key] = (IGameComponent)item.Value.Clone();
        }
    }
}

