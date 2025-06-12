namespace TermRTS.Test;

public class StorageTestData : TheoryData<IStorage>
{
    public StorageTestData()
    {
        var storage = new MappedCollectionStorage();
        storage.AddComponents([
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
        Add(storage);
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
}

internal class ComponentA(int entityId) : ComponentBase(entityId)
{
}

internal class ComponentB(int entityId) : ComponentBase(entityId)
{
}