namespace TermRTS;

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
public abstract class System<TWorld, TComponents> where TComponents : Enum
{
    // NOTE: If possible, enforce that otherEs are immutable
    // TODO: Split processing into iterating and processing strategies.
    //       Iteration should be optional to overwrite and encapsulate the processing step
    public abstract Dictionary<TComponents, IComponent>? ProcessComponents(
            UInt64 timeStepSizeMs,
            EntityBase<TComponents> thisEntityComponents,
            List<EntityBase<TComponents>> otherEntityComponents,
            ref TWorld world);
}
