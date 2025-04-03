namespace TermRTS.Data;

internal static class CachedEnumerable
{
    public static IEnumerable<T> ToCachedEnumerable<T>(this IEnumerable<T> items)
    {
        var enumerator = items.GetEnumerator();
        var cache = new List<T>();
        return ToCachedEnumerableHelper(enumerator, cache);
    }

    private static IEnumerable<T> ToCachedEnumerableHelper<T>(IEnumerator<T> enumerator, List<T> cache)
    {
        for (var i = 0;; i++)
            if (i < cache.Count)
            {
                yield return cache[i];
            }
            else if (enumerator.MoveNext())
            {
                var t = enumerator.Current;
                cache.Add(t);
                yield return t;
            }
            else
            {
                break;
            }
    }
}