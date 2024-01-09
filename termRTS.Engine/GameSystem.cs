namespace termRTS.Engine;

/// <summary>
/// A System defines one or multiple required components and processes <c>Entity</c>s which
/// provide all of these.
/// </summary>
/// <typeparam name="TWorld">
/// Type of the game world class.
/// </typeparam>
/// <typeparam name="TComponents">
/// Type of the enum listing all component types.
/// </typeparam>
public abstract class GameSystem<TWorld, TComponents> where TComponents : Enum
{
    // TODO: Process entities and world in two-state fashion.
    // TODO: If possible, enforce that otherEs are immutable
    public abstract void ProcessComponents(
            GameEntity<TComponents> thisEntityComponents,
            List<GameEntity<TComponents>> otherEntityComponents,
            ref TWorld world);
}
