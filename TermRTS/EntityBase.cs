namespace TermRTS;

/// <summary>
///     Base class for simulation entities, providing facilities for registering components.
/// </summary>
/// <typeparam name="TSystems">
///     Type of the Enum listing all system types.
/// </typeparam>
public class EntityBase
{
    #region Public Fields

    /// <summary>
    ///     Storage for one component per system type.
    /// </summary>
    public Dictionary<Type, IComponent> Components { get; } = new();

    #endregion

    /// <summary>
    ///     Property to indicate whether this entity is to be removed.
    /// </summary>
    public bool IsMarkedForRemoval { get; set; } = false;

    /// <summary>
    ///     Add a new component to this entity.
    /// </summary>
    public void AddComponent(IComponent component)
    {
        Components.Add(component.GetType(), component);
    }

    /// <summary>
    ///     Add or overwrite a component of the given system type.
    /// </summary>
    public void SetComponent(IComponent component)
    {
        Components[component.GetType()] = component;
    }

    /// <summary>
    ///     Checks whether this entity matches all given system types.
    /// </summary>
    /// <param name="systemTypes"> Collection of system types to match. </param>
    public bool IsCompatibleWith(Type[] systemTypes)
    {
        return systemTypes.All(Components.ContainsKey);
    }

    #region Constructors

    /// <summary>
    ///     Constructor
    /// </summary>
    public EntityBase()
    {
        Components = new Dictionary<Type, IComponent>();
    }

    /// <summary>
    ///     Shorthand for instantiating an entity with a single component
    /// </summary>
    public EntityBase(IComponent component)
    {
        Components = new Dictionary<Type, IComponent> { { component.GetType(), component } };
    }

    #endregion
}