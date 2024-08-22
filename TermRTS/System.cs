namespace TermRTS;

/// <summary>
///     A System defines one or multiple required components and processes <c>Entity</c>s which
///     provide all of these.
/// </summary>
/// <typeparam name="TWorld">
///     Type of the game world class.
/// </typeparam>
public abstract class System<TWorld>
{
    // NOTE: If possible, enforce that otherEs are immutable.
    // TODO: Use `in` keyword for entity list.
    // TODO: Split processing into iterating and processing strategies.
    // TODO: Iteration should be optional to overwrite and encapsulate the processing step
    // Alternative:
    // TODO: Encapsulate entities in handler class which allows different methods of iteration
    public abstract Dictionary<Type, IComponent>? ProcessComponents(
        ulong timeStepSizeMs,
        EntityBase thisEntityComponents,
        IEnumerable<EntityBase> otherEntityComponents,
        ref TWorld world);
}