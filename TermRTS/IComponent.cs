namespace TermRTS;

public interface IComponent : ICloneable
{
    int EntityId { get; }
}