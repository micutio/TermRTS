using System.Collections;
using System.Collections.Concurrent;

namespace TermRTS;

public class EventQueue<TElement, TPriority> : IProducerConsumerCollection<(TElement, TPriority)>
{
    #region Constructor

    /// <summary>
    ///     Constructor.
    /// </summary>
    /// <param name="comparer"> Comparer to facilitate sorting by priority. </param>
    public EventQueue(IComparer<TPriority>? comparer = default)
    {
        SyncRoot = new object();
        comparer ??= Comparer<TPriority>.Default;
        _queue = new PriorityQueue<TElement, (TPriority, long)>(Comparer<(TPriority, long)>.Create(
            (x, y) =>
            {
                var result = comparer.Compare(x.Item1, y.Item1);
                if (result == 0) result = x.Item2.CompareTo(y.Item2);
                return result;
            }));
    }

    #endregion

    #region Private Fields

    private readonly PriorityQueue<TElement, (TPriority, long)> _queue;
    private long _index;

    #endregion

    #region Properties

    /// <inheritdoc />
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

    /// <inheritdoc />
    public bool IsSynchronized => true; // use to be `false`, not sure whether true is correct

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public bool TryAdd((TElement, TPriority) item)
    {
        lock (SyncRoot)
        {
            _queue.Enqueue(item.Item1, (item.Item2, ++_index));
        }

        return true;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public object SyncRoot { get; }

    /// <inheritdoc />
    public void CopyTo((TElement, TPriority)[] array, int index)
    {
        throw new NotSupportedException();

        /*
        ArgumentNullException.ThrowIfNull(array);

        ArgumentOutOfRangeException.ThrowIfNegative(index);

        var count = Count;
        if (array.Length - index < count)
            throw new ArgumentException("Not enough elements after index in the destination array.");

        lock (SyncRoot)
        {
            for (var i = 0; i < count; ++i)
                array[i + index] = _queue[i];
        }
        */
    }

    /// <inheritdoc />
    public void CopyTo(Array array, int index)
    {
        throw new NotSupportedException();
        /*
        ArgumentNullException.ThrowIfNull(array);

        if (array is not (TElement, TPriority)[] pArray)
            throw new ArgumentException("Cannot convert to priority array", nameof(array));

        CopyTo(pArray, index);
        */
    }

    /// <inheritdoc />
    public IEnumerator<(TElement, TPriority)> GetEnumerator()
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public (TElement, TPriority)[] ToArray()
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    #endregion
}