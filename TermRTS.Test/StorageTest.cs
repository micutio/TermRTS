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
        storage.GetForType(typeof(ComponentA), out var components);
        Assert.Empty(components);
    }
    
    [Theory]
    [ClassData(typeof(StorageTestData))]
    public void TestInsertion(IStorage storage)
    {
        // Test retrieval by type
        storage.GetForType(typeof(ComponentA), out var componentsA);
        Assert.Equal(7, componentsA.Count());
        storage.GetForType(typeof(ComponentB), out var componentsB);
        Assert.Equal(3, componentsB.Count());
        
        // Test retrieval by entity
        storage.GetForEntity(1, out var e1Components);
        Assert.Equal(4, e1Components.Count());
        storage.GetForEntity(2, out var e2Components);
        Assert.Single(e2Components);
        storage.GetForEntity(3, out var e3Components);
        Assert.Equal(2, e3Components.Count());
        storage.GetForEntity(4, out var e4Components);
        Assert.Equal(3, e4Components.Count());
        
        // Test retrieval by entity and type
        storage.GetForEntityAndType(1, typeof(ComponentA), out var entity1TypeA);
        Assert.Equal(3, entity1TypeA.Count());
        storage.GetForEntityAndType(1, typeof(ComponentB), out var entity1TypeB);
        Assert.Single(entity1TypeB);
        storage.GetForEntityAndType(2, typeof(ComponentA), out var entity2TypeA);
        Assert.Single(entity2TypeA);
        storage.GetForEntityAndType(2, typeof(ComponentB), out var entity2TypeB);
        Assert.Empty(entity2TypeB);
        storage.GetForEntityAndType(3, typeof(ComponentA), out var entity3TypeA);
        Assert.Equal(2, entity3TypeA.Count());
        storage.GetForEntityAndType(3, typeof(ComponentB), out var entity3TypeB);
        Assert.Empty(entity3TypeB);
        storage.GetForEntityAndType(4, typeof(ComponentA), out var entity4TypeA);
        Assert.Single(entity4TypeA);
        storage.GetForEntityAndType(4, typeof(ComponentB), out var entity4TypeB);
        Assert.Equal(2, entity4TypeB.Count());
    }
    
    [Theory]
    [ClassData(typeof(StorageTestData))]
    public void TestRemoval(IStorage storage)
    {
        storage.GetForType(typeof(ComponentA), out var componentsA);
        Assert.Equal(7, componentsA.Count());
        storage.GetForType(typeof(ComponentB), out var componentsB);
        Assert.Equal(3, componentsB.Count());
        
        storage.RemoveComponentsByEntity(1);
        storage.GetForEntity(1, out var entity);
        Assert.Empty(entity);
        
        storage.RemoveComponentsByType(typeof(ComponentB));
        storage.GetForType(typeof(ComponentA), out var componentsAa);
        Assert.Equal(4, componentsAa.Count());
    }
}

internal class ComponentA(int entityId) : ComponentBase(entityId)
{
}

internal class ComponentB(int entityId) : ComponentBase(entityId)
{
}