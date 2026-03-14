namespace TermRTS;

public abstract class ComponentBase(int entityId)
{
    private readonly List<IDoubleBufferedProperty> _doubleBufferedProperties = [];

    public int EntityId { get; } = entityId;

    /// <summary>
    ///     Commits all double-buffered properties: the read side is updated to the current write side.
    ///     Called by storage at end of tick; until then, Get() returns the previous tick's value.
    /// </summary>
    public void SwapBuffers()
    {
        foreach (var property in _doubleBufferedProperties) property.SwitchBuffer();
    }

    protected void RegisterDoubleBufferedProperty(IDoubleBufferedProperty property)
    {
        _doubleBufferedProperties.Add(property);
    }
}
