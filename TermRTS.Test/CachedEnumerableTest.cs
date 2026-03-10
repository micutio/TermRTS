namespace TermRTS.Test;

/// <summary>
///     Tests for CachedEnumerable behavior via MappedCollectionStorage.GetAllForType,
///     which returns a cached enumerable.
/// </summary>
public class CachedEnumerableTest
{
    [Fact]
    public void Multiple_enumerations_return_same_sequence()
    {
        var storage = new MappedCollectionStorage();
        storage.AddComponent(new ComponentA(1));
        storage.AddComponent(new ComponentA(2));
        storage.AddComponent(new ComponentA(3));

        var list1 = storage.GetAllForType<ComponentA>().ToList();
        var list2 = storage.GetAllForType<ComponentA>().ToList();

        Assert.Equal(3, list1.Count);
        Assert.Equal(3, list2.Count);
        Assert.Equal(list1.Select(c => c.EntityId).ToList(),
            list2.Select(c => c.EntityId).ToList());
    }

    [Fact]
    public void Partial_enumeration_does_not_break_second_enumerator()
    {
        var storage = new MappedCollectionStorage();
        storage.AddComponent(new ComponentA(10));
        storage.AddComponent(new ComponentA(20));
        storage.AddComponent(new ComponentA(30));

        using var e1 = storage.GetAllForType<ComponentA>().GetEnumerator();
        using var e2 = storage.GetAllForType<ComponentA>().GetEnumerator();

        Assert.True(e1.MoveNext());
        var firstId = e1.Current.EntityId;
        Assert.True(e1.MoveNext());
        var secondId = e1.Current.EntityId;
        // e1 has advanced twice; e2 not yet

        var fullFromE2 = new List<int>();
        while (e2.MoveNext())
            fullFromE2.Add(e2.Current.EntityId);

        Assert.Equal(3, fullFromE2.Count);
        Assert.Equal([10, 20, 30], fullFromE2);
        Assert.Equal(10, firstId);
        Assert.Equal(20, secondId);
    }

    [Fact]
    public void Cached_sequence_unchanged_after_storage_unchanged()
    {
        var storage = new MappedCollectionStorage();
        storage.AddComponent(new ComponentA(1));
        storage.AddComponent(new ComponentA(2));
        var first = storage.GetAllForType<ComponentA>().ToList();
        var second = storage.GetAllForType<ComponentA>().ToList();
        Assert.Equal(first.Count, second.Count);
        Assert.Equal(first[0].EntityId, second[0].EntityId);
        Assert.Equal(first[1].EntityId, second[1].EntityId);
    }
}