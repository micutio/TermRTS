namespace TermRTS.Test;

public class StorageTestData : TheoryData<IStorage>
{
    public StorageTestData()
    {
        var mappedStorage = new MappedCollectionStorage();
        mappedStorage.AddComponents([
            new ComponentA(1),
            // should be able to store multiple components of the same type per entity
            new ComponentA(1),
            new ComponentA(1),
            new ComponentA(2),
            new ComponentA(3),
            new ComponentA(3),
            new ComponentA(4),
            new ComponentB(1),
            new ComponentB(4),
            new ComponentB(4)
        ]);
        Add(mappedStorage);

        var contiguousStorage = new ContiguousStorage();
        contiguousStorage.AddComponents([
            new ComponentA(1),
            // should be able to store multiple components of the same type per entity
            new ComponentA(1),
            new ComponentA(1),
            new ComponentA(2),
            new ComponentA(3),
            new ComponentA(3),
            new ComponentA(4),
            new ComponentB(1),
            new ComponentB(4),
            new ComponentB(4)
        ]);
        Add(contiguousStorage);
    }
}

/// <summary>
///     Tests for storage:
///     - insert and retrieve by type/id
///     - insert some more and check Count() results for enumerators.
///     - delete and check whether is not existing
///     - cached queries
/// </summary>
public class StorageTest
{
    /// Sanity check.
    [Fact]
    public void TestTrue()
    {
        Assert.True(true);
    }

    [Fact]
    public void TestInitialState()
    {
        var storage = new MappedCollectionStorage();
        Assert.Empty(storage.GetAllForType<ComponentA>());
    }

    [Theory]
    [ClassData(typeof(StorageTestData))]
    public void TestInsertion(IReadonlyStorage storage)
    {
        // Test retrieval by type
        Assert.Equal(7, storage.GetAllForType<ComponentA>().Count());
        Assert.Equal(3, storage.GetAllForType<ComponentB>().Count());

        // Test retrieval by entity
        Assert.Equal(4, storage.GetAllForEntity(1).Count());
        Assert.Single(storage.GetAllForEntity(2));
        Assert.Equal(2, storage.GetAllForEntity(3).Count());
        Assert.Equal(3, storage.GetAllForEntity(4).Count());

        // Test retrieval by entity and type
        Assert.Equal(3, storage.GetAllForTypeAndEntity<ComponentA>(1).Count());
        Assert.Single(storage.GetAllForTypeAndEntity<ComponentB>(1));
        Assert.Single(storage.GetAllForTypeAndEntity<ComponentA>(2));
        Assert.Empty(storage.GetAllForTypeAndEntity<ComponentB>(2));
        Assert.Equal(2, storage.GetAllForTypeAndEntity<ComponentA>(3).Count());
        Assert.Empty(storage.GetAllForTypeAndEntity<ComponentB>(3));
        Assert.Single(storage.GetAllForTypeAndEntity<ComponentA>(4));
        Assert.Equal(2, storage.GetAllForTypeAndEntity<ComponentB>(4).Count());
    }

    [Theory]
    [ClassData(typeof(StorageTestData))]
    public void TestRemoval(IStorage storage)
    {
        Assert.Equal(7, storage.GetAllForType<ComponentA>().Count());
        Assert.Equal(3, storage.GetAllForType<ComponentB>().Count());

        storage.RemoveComponentsByEntity(1);
        Assert.Empty(storage.GetAllForEntity(1));

        storage.RemoveComponentsByType(typeof(ComponentB));
        Assert.Equal(4, storage.GetAllForType<ComponentA>().Count());
    }

    [Fact]
    public void GetSingleForType_returns_default_when_no_component_of_type()
    {
        var storage = new MappedCollectionStorage();
        Assert.Null(storage.GetSingleForType<ComponentA>());
    }

    [Fact]
    public void GetSingleForType_returns_first_when_one_component_exists()
    {
        var storage = new MappedCollectionStorage();
        var c = new ComponentA(99);
        storage.AddComponent(c);
        var single = storage.GetSingleForType<ComponentA>();
        Assert.NotNull(single);
        Assert.Equal(99, single.EntityId);
    }

    [Theory]
    [ClassData(typeof(StorageTestData))]
    public void GetSingleForType_returns_first_when_multiple_exist(IReadonlyStorage storage)
    {
        var single = storage.GetSingleForType<ComponentA>();
        Assert.NotNull(single);
        Assert.Contains(single, storage.GetAllForType<ComponentA>());
    }

    [Fact]
    public void GetSingleForTypeAndEntity_returns_default_when_entity_missing()
    {
        var storage = new MappedCollectionStorage();
        storage.AddComponent(new ComponentA(1));
        Assert.Null(storage.GetSingleForTypeAndEntity<ComponentA>(999));
    }

    [Fact]
    public void GetSingleForTypeAndEntity_returns_default_when_type_missing_for_entity()
    {
        var storage = new MappedCollectionStorage();
        storage.AddComponent(new ComponentA(1));
        Assert.Null(storage.GetSingleForTypeAndEntity<ComponentB>(1));
    }

    [Fact]
    public void GetSingleForTypeAndEntity_returns_component_when_one_exists()
    {
        var storage = new MappedCollectionStorage();
        var c = new ComponentA(42);
        storage.AddComponent(c);
        var single = storage.GetSingleForTypeAndEntity<ComponentA>(42);
        Assert.NotNull(single);
        Assert.Equal(42, single.EntityId);
    }

    [Fact]
    public void GetSingleForTypeAndEntity_returns_first_when_multiple_exist_for_entity()
    {
        var storage = new MappedCollectionStorage();
        storage.AddComponent(new ComponentA(1));
        storage.AddComponent(new ComponentA(1));
        var single = storage.GetSingleForTypeAndEntity<ComponentA>(1);
        Assert.NotNull(single);
        Assert.Equal(1, single.EntityId);
        Assert.Equal(2, storage.GetAllForTypeAndEntity<ComponentA>(1).Count());
    }

    [Fact]
    public void Clear_removes_all_components_and_cache()
    {
        var storage = new MappedCollectionStorage();
        storage.AddComponent(new ComponentA(1));
        storage.AddComponent(new ComponentB(1));
        Assert.Single(storage.GetAllForType<ComponentA>());
        Assert.Single(storage.GetAllForType<ComponentB>());

        storage.Clear();

        Assert.Empty(storage.GetAllForType<ComponentA>());
        Assert.Empty(storage.GetAllForType<ComponentB>());
        Assert.Empty(storage.GetAllForEntity(1));
        Assert.Null(storage.GetSingleForType<ComponentA>());
        Assert.Null(storage.GetSingleForTypeAndEntity<ComponentA>(1));
    }

    [Fact]
    public void Clear_allows_adding_components_afterward()
    {
        var storage = new MappedCollectionStorage();
        storage.AddComponent(new ComponentA(1));
        storage.Clear();
        storage.AddComponent(new ComponentA(2));
        var single = storage.GetSingleForType<ComponentA>();
        Assert.NotNull(single);
        Assert.Equal(2, single.EntityId);
    }

    [Fact]
    public void SwapBuffers_updates_read_value_of_double_buffered_component_after_call()
    {
        var storage = new MappedCollectionStorage();
        var component = new ComponentWithDoubleBuffer(1, 10);
        storage.AddComponent(component);

        Assert.Equal(10, component.GetValue());
        component.SetValue(42);
        Assert.Equal(10, component.GetValue());

        storage.SwapBuffers();
        Assert.Equal(42, component.GetValue());
    }

    // --- Cache invalidation (plan §3) ---

    [Fact]
    public void Cache_invalidation_after_AddComponent_new_enumeration_includes_new_component()
    {
        var storage = new MappedCollectionStorage();
        storage.AddComponent(new ComponentA(1));
        var first = storage.GetAllForType<ComponentA>().ToList();
        Assert.Single(first);

        storage.AddComponent(new ComponentA(2));
        var second = storage.GetAllForType<ComponentA>().ToList();
        Assert.Equal(2, second.Count);
        Assert.Contains(second, c => c.EntityId == 1);
        Assert.Contains(second, c => c.EntityId == 2);
    }

    [Fact]
    public void Cache_invalidation_after_RemoveComponentsByEntity_no_enumeration_returns_that_entity_components()
    {
        var storage = new MappedCollectionStorage();
        storage.AddComponent(new ComponentA(1));
        storage.AddComponent(new ComponentA(2));
        storage.AddComponent(new ComponentB(1));
        Assert.Equal(2, storage.GetAllForType<ComponentA>().Count());
        Assert.Single(storage.GetAllForType<ComponentB>());

        storage.RemoveComponentsByEntity(1);
        Assert.Empty(storage.GetAllForEntity(1));
        var asList = storage.GetAllForType<ComponentA>().ToList();
        Assert.Single(asList);
        Assert.Equal(2, asList[0].EntityId);
        Assert.Empty(storage.GetAllForType<ComponentB>());
    }

    /// <summary>
    /// After removing an entity that had components, adding the same type again should yield
    /// correct count. With the fix, the store does not retain empty lists for removed entities.
    /// </summary>
    [Fact]
    public void RemoveComponentsByEntity_does_not_leave_empty_lists_public_api_check()
    {
        var storage = new MappedCollectionStorage();
        var e1 = new EntityBase();
        var e2 = new EntityBase();
        storage.AddComponent(new ComponentA(e1.Id));
        storage.AddComponent(new ComponentA(e2.Id));
        Assert.Equal(2, storage.GetAllForType<ComponentA>().Count());

        storage.RemoveComponentsByEntity(e1.Id);
        Assert.Single(storage.GetAllForType<ComponentA>().ToList());

        storage.AddComponent(new ComponentA(e1.Id));
        Assert.Equal(2, storage.GetAllForType<ComponentA>().Count());
        // Stress: remove both, add one new entity's component — count must be 1
        storage.RemoveComponentsByEntity(e1.Id);
        storage.RemoveComponentsByEntity(e2.Id);
        var e3 = new EntityBase();
        storage.AddComponent(new ComponentA(e3.Id));
        Assert.Single(storage.GetAllForType<ComponentA>().ToList());
        Assert.Equal(e3.Id, storage.GetSingleForType<ComponentA>()!.EntityId);
    }

    // --- GetListForType (list view API) ---

    [Fact]
    public void GetListForType_returns_empty_list_when_no_components_of_type()
    {
        var storage = new MappedCollectionStorage();
        var list = storage.GetListForType<ComponentA>();
        Assert.NotNull(list);
        Assert.Empty(list);
    }

    [Theory]
    [ClassData(typeof(StorageTestData))]
    public void GetListForType_matches_GetAllForType_count_and_content(IReadonlyStorage storage)
    {
        var asList = storage.GetListForType<ComponentA>();
        var asEnum = storage.GetAllForType<ComponentA>().ToList();
        Assert.Equal(asEnum.Count, asList.Count);
        for (var i = 0; i < asList.Count; i++)
            Assert.Same(asEnum[i], asList[i]);
    }

    [Fact]
    public void GetListForType_is_invalidated_after_AddComponent()
    {
        var storage = new MappedCollectionStorage();
        storage.AddComponent(new ComponentA(1));
        var first = storage.GetListForType<ComponentA>();
        Assert.Single(first);
        storage.AddComponent(new ComponentA(2));
        var second = storage.GetListForType<ComponentA>();
        Assert.Equal(2, second.Count);
    }

    // --- TryGetSingle (plan §3) ---

    [Fact]
    public void TryGetSingleForType_returns_false_and_default_when_absent()
    {
        var storage = new MappedCollectionStorage();
        var found = storage.TryGetSingleForType<ComponentA>(out var component);
        Assert.False(found);
        Assert.Null(component);
    }

    [Fact]
    public void TryGetSingleForType_returns_true_and_instance_when_one_exists()
    {
        var storage = new MappedCollectionStorage();
        var c = new ComponentA(42);
        storage.AddComponent(c);
        var found = storage.TryGetSingleForType<ComponentA>(out var component);
        Assert.True(found);
        Assert.NotNull(component);
        Assert.Equal(42, component!.EntityId);
    }

    [Theory]
    [ClassData(typeof(StorageTestData))]
    public void TryGetSingleForType_returns_true_and_first_when_multiple_exist(IReadonlyStorage storage)
    {
        var found = storage.TryGetSingleForType<ComponentA>(out var component);
        Assert.True(found);
        Assert.NotNull(component);
        Assert.Contains(component, storage.GetAllForType<ComponentA>());
    }

    [Fact]
    public void TryGetSingleForTypeAndEntity_returns_false_when_entity_missing()
    {
        var storage = new MappedCollectionStorage();
        storage.AddComponent(new ComponentA(1));
        var found = storage.TryGetSingleForTypeAndEntity<ComponentA>(999, out var component);
        Assert.False(found);
        Assert.Null(component);
    }

    [Fact]
    public void TryGetSingleForTypeAndEntity_returns_false_when_type_missing_for_entity()
    {
        var storage = new MappedCollectionStorage();
        storage.AddComponent(new ComponentA(1));
        var found = storage.TryGetSingleForTypeAndEntity<ComponentB>(1, out var component);
        Assert.False(found);
        Assert.Null(component);
    }

    [Fact]
    public void TryGetSingleForTypeAndEntity_returns_true_and_component_when_exists()
    {
        var storage = new MappedCollectionStorage();
        var c = new ComponentA(42);
        storage.AddComponent(c);
        var found = storage.TryGetSingleForTypeAndEntity<ComponentA>(42, out var component);
        Assert.True(found);
        Assert.NotNull(component);
        Assert.Equal(42, component!.EntityId);
    }
}

internal class ComponentA(int entityId) : ComponentBase(entityId)
{
}

internal class ComponentB(int entityId) : ComponentBase(entityId)
{
}

/// <summary>
///     Component with a double-buffered value for testing SwapBuffers.
/// </summary>
internal class ComponentWithDoubleBuffer : ComponentBase
{
    private readonly DoubleBuffered<int> _value;

    public ComponentWithDoubleBuffer(int entityId, int initial = 0) : base(entityId)
    {
        _value = new DoubleBuffered<int>(initial);
        RegisterDoubleBufferedProperty(_value);
    }

    public void SetValue(int v)
    {
        _value.Set(v);
    }

    public int GetValue()
    {
        return _value.Get();
    }
}