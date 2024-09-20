namespace TermRTS;

/// <summary>
///     Base class for simulation entities, providing facilities for registering components.
/// </summary>
public class EntityBase
{
    #region Fields

    private static int _runningId;

    #endregion

    #region Constructors

    /// <summary>
    ///     Constructor
    /// </summary>
    public EntityBase()
    {
        Components = new Dictionary<Type, ComponentBase>();
    }

    /// <summary>
    ///     Shorthand for instantiating an entity with a single component
    /// </summary>
    public EntityBase(ComponentBase component)
    {
        Id = Interlocked.Increment(ref _runningId);
        Components = new Dictionary<Type, ComponentBase> { { component.GetType(), component } };
    }

    #endregion

    #region Properties

    public int Id { get; }

    /// <summary>
    ///     Storage for one component per system type.
    /// </summary>
    public Dictionary<Type, ComponentBase> Components { get; }

    /// <summary>
    ///     Property to indicate whether this entity is to be removed.
    /// </summary>
    public bool IsMarkedForRemoval { get; set; } = false;

    #endregion

    #region Public Methods

    /// <summary>
    ///     Add a new component to this entity.
    /// </summary>
    public void AddComponent(ComponentBase component)
    {
        Components.Add(component.GetType(), component);
    }

    /// <summary>
    ///     Add or overwrite a component of the given system type.
    /// </summary>
    public void SetComponent(ComponentBase component)
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

    #endregion
}