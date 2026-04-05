using System.Text.Json.Serialization;

namespace TermRTS.Examples.Greenery.Ecs.Component;

[method: JsonConstructor]
public class FovChunk(int entityId, int cx, int cy, Memory<bool> fovField)
    : ComponentBase(entityId)
{
    public int Cx { get; } = cx;
    public int Cy { get; } = cy;
    public Memory<bool> FovField { get; } = fovField;
}