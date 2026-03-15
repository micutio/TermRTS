namespace TermRTS.Event;

public class EventQueue<TElement, TPriority>
{
    #region Constructor

    /// <summary>
    ///     Constructor.
    /// </summary>
    public EventQueue()
    {
        var comparer = Comparer<TPriority>.Default;
        _queue = new PriorityQueue<TElement, (TPriority, long)>(
            Comparer<(TPriority, long)>.Create((x, y) =>
            {
                var result = comparer.Compare(x.Item1, y.Item1);
                if (result == 0) result = x.Item2.CompareTo(y.Item2);
                return result;
            }));
    }

    #endregion

    #region Properties

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

    #endregion

    private object SyncRoot { get; } = new();

    internal List<(TElement, TPriority)> GetSerializableElements()
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

    #region Fields

    private readonly PriorityQueue<TElement, (TPriority, long)> _queue;
    private long _index;

    #endregion

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

    /// <summary>
    ///     Under one lock, peeks at the front; if <paramref name="isDue"/> returns true for the
    ///     priority, dequeues and returns the item. Reduces lock acquisitions when processing
    ///     multiple due events.
    /// </summary>
    public bool TryTakeIf(Func<TPriority, bool> isDue, out (TElement, TPriority) item)
    {
        lock (SyncRoot)
        {
            if (!_queue.TryPeek(out var element, out var priorityTuple))
            {
                item = default;
                return false;
            }

            if (!isDue(priorityTuple.Item1))
            {
                item = default;
                return false;
            }

            if (_queue.TryDequeue(out element, out priorityTuple))
            {
                item = (element, priorityTuple.Item1);
                return true;
            }

            item = default;
            return false;
        }
    }

    internal void Clear()
    {
        lock (SyncRoot)
        {
            _queue.Clear();
        }
    }

    #endregion
}