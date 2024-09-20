namespace TermRTS;

// TODO: support for double-buffered thread-safe properties

public abstract class ComponentBase(int entityId)
{
    public int EntityId { get; } = entityId;
}