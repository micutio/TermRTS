using System.Text.Json.Serialization;

namespace TermRTS;

public abstract class ComponentBase(int entityId)
{
    private readonly List<IDoubleBufferedProperty> _doubleBufferedProperties = [];
    public int EntityId { get; } = entityId;
    
    public void SwapBuffers()
    {
        foreach (var property in _doubleBufferedProperties) property.SwitchBuffer();
    }
    
    protected void RegisterDoubleBufferedProperty(IDoubleBufferedProperty property)
    {
        _doubleBufferedProperties.Add(property);
    }
}