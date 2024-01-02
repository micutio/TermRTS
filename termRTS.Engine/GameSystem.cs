namespace termRTS.Engine;

/// <summary>
/// A System defines one or multiple required components and processes <c>Entity</c>s which
/// provide all of these.
/// </summary>
/// <typeparam name="TW">
/// Type of the game world class.
/// </typeparam>
/// <typeparam name="T">
/// Type of the enum listing all component types.
/// </typeparam>
public abstract class GameSystem<TW, T> where T : Enum
{
    // TODO: Process entities and world in two-state fashion.
    // TODO: If possible, enforce that otherEs are immutable
    public abstract void ProcessComponents(
            Dictionary<T, IGameComponent> thisEntityComponents,
            List<Dictionary<T, IGameComponent>> otherEntityComponents,
            ref TW world);
}
