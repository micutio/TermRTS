using System.Text.Json.Serialization;

namespace TermRTS;

public abstract class ComponentBase(int entityId)
{
    [JsonInclude] internal readonly List<IDoubleBufferedProperty> DoubleBufferedProperties = [];
    public int EntityId { get; } = entityId;

    public void SwapBuffers()
    {
        foreach (var property in DoubleBufferedProperties) property.SwitchBuffer();
    }

    protected void RegisterDoubleBufferedProperty(IDoubleBufferedProperty property)
    {
        DoubleBufferedProperties.Add(property);
    }
}