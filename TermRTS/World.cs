namespace TermRTS;

/// <summary>
///     Interface for the game world. Should allow to apply change events
/// </summary>
public interface IWorld
{
    // TODO: Maybe rename to `Tick` or something else more sensible.
    public void ApplyChange();
}