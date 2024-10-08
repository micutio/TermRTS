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
        Id = Interlocked.Increment(ref _runningId);
    }
    
    /// <summary>
    ///     Shorthand for instantiating an entity with a single component
    /// </summary>
    public EntityBase(ComponentBase component)
    {
        Id = Interlocked.Increment(ref _runningId);
    }
    
    #endregion
    
    #region Properties
    
    public int Id { get; }
    
    /// <summary>
    ///     Property to indicate whether this entity is to be removed.
    /// </summary>
    public bool IsMarkedForRemoval { get; set; } = false;
    
    #endregion
}

/// <summary>
///     Interface to allow hiding types
/// </summary>
public interface IDoubleBufferedProperty
{
    public void SwitchBuffer();
}

/// <summary>
///     Implementation of a property with decoupled read and write operations.
///     The property can be reassigned a new value while still exposing the old value publicly.
///     Only the <see cref="SwitchBuffer" /> method updates the readable value.
/// </summary>
/// <param name="val">Value of the property</param>
/// <typeparam name="T">Type of the property</typeparam>
public class DoubleBuffered<T>(T val) : IDoubleBufferedProperty
{
    private T _buffer = val;
    private T _value = val;
    
    public void SwitchBuffer()
    {
        _buffer = _value;
    }
    
    public void Set(T value)
    {
        _value = value;
    }
    
    public T Get()
    {
        return _buffer;
    }
}