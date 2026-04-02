using TermRTS.Storage;

namespace TermRTS.Test;

/// <summary>
///     Captures the storage reference passed to systems during Tick so tests can assert on it.
/// </summary>
internal sealed class StorageSpySystem : ISimSystem
{
    public IReadonlyStorage? Storage { get; private set; }

    public void ProcessComponents(ulong timeStepSizeMs, in IReadonlyStorage storage)
    {
        Storage = storage;
    }
}

public class CoreTest
{
    [Theory]
    [ClassData(typeof(TestCoreParallelAndStorageConfigs))]
    public void AddEntity_and_AddComponent_appear_in_storage_after_Tick(Core core)
    {
        var spy = new StorageSpySystem();
        core.AddSimSystem(spy);

        var entity = new Entity();
        core.AddEntity(entity);
        core.AddComponent(new ComponentA(entity.Id));

        Assert.Null(spy.Storage);
        core.Tick(1);

        Assert.NotNull(spy.Storage);
        var components = spy.Storage!.GetAllForType<ComponentA>().ToList();
        Assert.Single(components);
        Assert.Equal(entity.Id, components[0].EntityId);
        Assert.Single(spy.Storage.GetAllForEntity(entity.Id));
    }

    [Theory]
    [ClassData(typeof(TestCoreParallelAndStorageConfigs))]
    public void Entity_marked_for_removal_is_removed_after_Tick_components_cleaned(Core core)
    {
        var spy = new StorageSpySystem();
        core.AddSimSystem(spy);

        var entity = new Entity();
        core.AddEntity(entity);
        core.AddComponent(new ComponentA(entity.Id));
        core.Tick(1);

        Assert.NotNull(spy.Storage);
        Assert.Single(spy.Storage!.GetAllForType<ComponentA>());
        Assert.Single(spy.Storage.GetAllForEntity(entity.Id));

        entity.IsMarkedForRemoval = true;
        core.Tick(1);

        Assert.Empty(spy.Storage.GetAllForEntity(entity.Id));
        Assert.Empty(spy.Storage.GetAllForType<ComponentA>());
    }

    [Theory]
    [ClassData(typeof(TestCoreParallelAndStorageConfigs))]
    public void AddAllEntities_and_AddAllComponents_appear_after_Tick(Core core)
    {
        var spy = new StorageSpySystem();
        core.AddSimSystem(spy);

        var e1 = new Entity();
        var e2 = new Entity();
        core.AddEntities([e1, e2]);
        core.AddComponents([
            new ComponentA(e1.Id),
            new ComponentB(e1.Id),
            new ComponentA(e2.Id)
        ]);

        core.Tick(1);

        Assert.NotNull(spy.Storage);
        Assert.Equal(2, spy.Storage!.GetAllForType<ComponentA>().Count());
        Assert.Single(spy.Storage.GetAllForType<ComponentB>());
        Assert.Equal(2, spy.Storage.GetAllForEntity(e1.Id).Count());
        Assert.Single(spy.Storage.GetAllForEntity(e2.Id));
    }

    /// <summary>
    /// Component added for an existing entity (already in the sim) appears on next tick.
    /// </summary>
    [Theory]
    [ClassData(typeof(TestCoreParallelAndStorageConfigs))]
    public void Deferred_add_component_for_existing_entity_appears_after_Tick(Core core)
    {
        var spy = new StorageSpySystem();
        core.AddSimSystem(spy);

        var entity = new Entity();
        core.AddEntity(entity);
        core.AddComponent(new ComponentA(entity.Id));
        core.Tick(1);

        Assert.NotNull(spy.Storage);
        Assert.Single(spy.Storage!.GetAllForType<ComponentA>());

        core.AddComponent(new ComponentB(entity.Id));
        Assert.Empty(spy.Storage.GetAllForType<ComponentB>()); // not yet visible
        core.Tick(1);
        Assert.Single(spy.Storage.GetAllForType<ComponentB>().ToList());
        Assert.Equal(entity.Id,
            spy.Storage.GetSingleForTypeAndEntity<ComponentB>(entity.Id)!.EntityId);
    }
}