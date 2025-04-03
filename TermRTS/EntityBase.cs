using System.Text.Json.Serialization;

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
    ///     Create a new EntityBase with incremental running Id.
    /// </summary>
    public EntityBase()
    {
        Id = Interlocked.Increment(ref _runningId);
    }

    [JsonConstructor]
    internal EntityBase(int id)
    {
        Id = id;
        // Set the running id to be greater than the current id,
        // to avoid collisions for future Id initialisations.
        _runningId = Interlocked.Increment(ref id);
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
/// <param name="value">Value of the property</param>
/// <typeparam name="T">Type of the property</typeparam>
public class DoubleBuffered<T>(T value) : IDoubleBufferedProperty
{
    private T _buffer = value;

    private T _value = value;

    #region IDoubleBufferedProperty Members

    public void SwitchBuffer()
    {
        _buffer = _value;
    }

    #endregion

    public void Set(T newValue)
    {
        _value = newValue;
    }

    public T Get()
    {
        return _buffer;
    }
}