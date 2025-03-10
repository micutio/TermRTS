using System.Text.Json.Serialization;

namespace TermRTS.Event;

public class EventQueue<TElement, TPriority>
{
    #region Private Fields
    
    private readonly PriorityQueue<TElement, (TPriority, long)> _queue;
    private long _index;
    
    #endregion
    
    #region Constructor
    
    /// <summary>
    ///     Constructor.
    /// </summary>
    public EventQueue()
    {
        var comparer = Comparer<TPriority>.Default;
        _queue = new PriorityQueue<TElement, (TPriority, long)>(
            Comparer<(TPriority, long)>.Create(
                (x, y) =>
                {
                    var result = comparer.Compare(x.Item1, y.Item1);
                    if (result == 0) result = x.Item2.CompareTo(y.Item2);
                    return result;
                }));
    }
    
    public EventQueue(IEnumerable<(TElement, TPriority)> serializedElements) : this()
    {
        foreach (var e in serializedElements) TryAdd(e);
    }
    
    #endregion
    
    
    #region Properties
    
    [JsonIgnore]
    public int Count
    {
        get
        {
            lock (SyncRoot)
            {
                return _queue.Count;
            }
        }
    }
    
    [JsonInclude]
    public ICollection<(TElement, TPriority)> SerializedElements
    {
        get
        {
            lock (SyncRoot)
            {
                return _queue
                    .UnorderedItems
                    .AsEnumerable()
                    .Select(e =>
                    {
                        var (element, (priority, _)) = e;
                        return (Element: element, priority);
                    })
                    .ToList();
            }
        }
    }
    
    #endregion
    
    [JsonIgnore] private object SyncRoot { get; } = new();
    
    #region Public Methods
    
    public bool TryAdd((TElement, TPriority) item)
    {
        lock (SyncRoot)
        {
            _queue.Enqueue(item.Item1, (item.Item2, ++_index));
        }
        
        return true;
    }
    
    public bool TryTake(out (TElement, TPriority) item)
    {
        lock (SyncRoot)
        {
            if (_queue.TryDequeue(out var element, out var priority))
            {
                item = (element, priority.Item1);
                return true;
            }
            
            item = default;
            return false;
        }
    }
    
    public bool TryPeek(out TElement? element, out TPriority? priority)
    {
        lock (SyncRoot)
        {
            var value = _queue.TryPeek(out var element1, out var priority1);
            element = element1;
            priority = priority1.Item1;
            return value;
        }
    }
    
    #endregion
}