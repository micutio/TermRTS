// https://stackoverflow.com/questions/23470196/concurrent-collection-with-priority

using System.Collections;

// https://stackoverflow.com/questions/23470196/concurrent-collection-with-priority

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace termRTS.Engine;

public class EventQueue<TElement, TPriority> : IProducerConsumerCollection<(TElement, TPriority)>
{
    #region Private Fields

    private readonly PriorityQueue<TElement, (TPriority, long)> _queue;
    private long _index = 0L;

    #endregion

    #region Constructor

    public EventQueue(IComparer<TPriority>? comparer = default)
    {
        comparer ??= Comparer<TPriority>.Default;
        _queue = new PriorityQueue<TElement, (TPriority, long)>(Comparer<(TPriority, long)>.Create((x, y) =>
        {
            var result = comparer.Compare(x.Item1, y.Item1);
            if (result == 0) result = x.Item2.CompareTo(y.Item2);
            return result;
        }));
    }

    #endregion

    #region Public Methods

    public int Count
    {
        get { lock (_queue) return _queue.Count; }
    }

    public bool IsSynchronized => false;

    public bool TryAdd((TElement, TPriority) item)
    {
        lock (_queue) _queue.Enqueue(item.Item1, (item.Item2, ++_index));
        return true;
    }

    public bool TryTake([MaybeNullWhen(false)] out (TElement, TPriority) item)
    {
        lock (_queue)
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


    public object SyncRoot => throw new NotSupportedException();

    public void CopyTo((TElement, TPriority)[] array, int index)
    {
        throw new NotSupportedException();
    }

    public void CopyTo(Array array, int index)
    {
        throw new NotSupportedException();
    }

    public IEnumerator<(TElement, TPriority)> GetEnumerator()
    {
        throw new NotSupportedException();
    }

    public (TElement, TPriority)[] ToArray()
    {
        throw new NotSupportedException();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion
}
