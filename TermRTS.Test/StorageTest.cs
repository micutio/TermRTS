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
/// Tests for storage:
/// - insert and retrieve by type/id
/// - insert some more and check Count() results for enumerators.
/// - delete and check whether is not existing
/// - cached queries
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
        Assert.Empty(storage.GetForType(typeof(ComponentA)));
    }
    
    [Theory]
    [ClassData(typeof(StorageTestData))]
    public void TestInsertion(IStorage storage)
    {
        // Test retrieval by type
        Assert.Equal(7, storage.GetForType(typeof(ComponentA)).Count());
        Assert.Equal(3, storage.GetForType(typeof(ComponentB)).Count());
        
        // Test retrieval by entity
        Assert.Equal(4, storage.GetForEntity(1).Count());
        Assert.Single(storage.GetForEntity(2));
        Assert.Equal(2, storage.GetForEntity(3).Count());
        Assert.Equal(3, storage.GetForEntity(4).Count());
        
        // Test retrieval by entity and type
        Assert.Equal(3, storage.GetForEntityAndType(1, typeof(ComponentA)).Count());
        Assert.Single(storage.GetForEntityAndType(1, typeof(ComponentB)));
        Assert.Single(storage.GetForEntityAndType(2, typeof(ComponentA)));
        Assert.Empty(storage.GetForEntityAndType(2, typeof(ComponentB)));
        Assert.Equal(2, storage.GetForEntityAndType(3, typeof(ComponentA)).Count());
        Assert.Empty(storage.GetForEntityAndType(3, typeof(ComponentB)));
        Assert.Single(storage.GetForEntityAndType(4, typeof(ComponentA)));
        Assert.Equal(2, storage.GetForEntityAndType(4, typeof(ComponentB)).Count());
    }
    
    [Theory]
    [ClassData(typeof(StorageTestData))]
    public void TestRemoval(IStorage storage)
    {
        Assert.Equal(7, storage.GetForType(typeof(ComponentA)).Count());
        Assert.Equal(3, storage.GetForType(typeof(ComponentB)).Count());
        
        storage.RemoveComponentsByEntity(1);
        Assert.Empty(storage.GetForEntity(1));
        
        storage.RemoveComponentsByType(typeof(ComponentB));
        Assert.Equal(4, storage.GetForType(typeof(ComponentA)).Count());
    }
}

internal class ComponentA(int entityId) : ComponentBase(entityId)
{
}

internal class ComponentB(int entityId) : ComponentBase(entityId)
{
}