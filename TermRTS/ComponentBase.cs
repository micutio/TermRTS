namespace TermRTS;

public interface IGameComponent
{
    string TypeName { get; }
    object GetCurrentStateUntyped();
    void SetCurrentStateUntyped(object state);
}

public abstract class ComponentBase
{
    private readonly List<IDoubleBufferedProperty> _doubleBufferedProperties = [];

    public int EntityId { get; internal set; }

    // Parameterless constructor to allow parameterless-instantiation (eg. used by registry deserializers)
    protected ComponentBase()
    {
        EntityId = 0;
    }

    protected ComponentBase(int entityId)
    {
        EntityId = entityId;
    }

    /// <summary>
    ///     Commits all double-buffered properties: the read side is updated to the current write side.
    ///     Called by storage at end of tick; until then, Get() returns the previous tick's value.
    /// </summary>
    public virtual void SwapBuffers()
    {
        foreach (var property in _doubleBufferedProperties) property.SwitchBuffer();
    }

    protected void RegisterDoubleBufferedProperty(IDoubleBufferedProperty property)
    {
        _doubleBufferedProperties.Add(property);
    }
}

public abstract class ComponentBase<TData> : ComponentBase, IGameComponent
    where TData : struct
{
    private TData _currentState;
    private TData _nextState;

    protected ComponentBase() : base()
    {
        _currentState = default;
        _nextState = default;
    }

    protected ComponentBase(int entityId) : base(entityId)
    {
        _currentState = default;
        _nextState = default;
    }

    public TData GetCurrentData() => _currentState;

    public void SetNextData(TData data)
    {
        _nextState = data;
    }

    /// <summary>
    /// Replace both current and next states (used during deserialization/initialization).
    /// </summary>
    public void ReplaceState(TData data)
    {
        _currentState = data;
        _nextState = data;
    }

    public override void SwapBuffers()
    {
        base.SwapBuffers();
        _currentState = _nextState;
    }

    object IGameComponent.GetCurrentStateUntyped() => _currentState!;

    void IGameComponent.SetCurrentStateUntyped(object state)
    {
        if (state is TData d)
            ReplaceState(d);
        else
            throw new InvalidCastException($"Invalid state type for component {GetType().Name}: {state?.GetType().FullName}");
    }

    public string TypeName => GetType().Name;
}