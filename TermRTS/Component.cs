namespace TermRTS;

// TODO: support for double-buffered thread-safe properties

public abstract class ComponentBase(int entityId)
{
    public int EntityId { get; } = entityId;
    
    private readonly List<IDoubleBufferedProperty> _doubleBufferedProperties = [];
    
    public void SwapBuffers()
    {
        foreach (var property in _doubleBufferedProperties)
        {
            property.SwitchBuffer();
        }
    }
    
    protected void RegisterDoubleBufferedProperty(IDoubleBufferedProperty property)
    {
        _doubleBufferedProperties.Add(property);
    }
}
