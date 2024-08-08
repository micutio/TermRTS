namespace TermRTS;

/// <summary>
///     A System defines one or multiple required components and processes <c>Entity</c>s which
///     provide all of these.
/// </summary>
/// <typeparam name="TWorld">
///     Type of the game world class.
/// </typeparam>
/// <typeparam name="TComponents">
///     Type of the enum listing all component types.
/// </typeparam>
public abstract class System<TWorld, TComponents> where TComponents : Enum
{
    // NOTE: If possible, enforce that otherEs are immutable.
    // TODO: Use `in` keyword for entity list.
    // TODO: Split processing into iterating and processing strategies.
    // TODO: Iteration should be optional to overwrite and encapsulate the processing step
    // Alternative:
    // TODO: Encapsulate entities in handler class which allows different methods of iteration
    public abstract Dictionary<TComponents, IComponent>? ProcessComponents(
        ulong timeStepSizeMs,
        EntityBase<TComponents> thisEntityComponents,
        IEnumerable<EntityBase<TComponents>> otherEntityComponents,
        ref TWorld world);
}