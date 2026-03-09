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
    [Fact]
    public void AddEntity_and_AddComponent_appear_in_storage_after_Tick()
    {
        var core = new Core { Renderer = new NullRenderer() };
        var spy = new StorageSpySystem();
        core.AddSimSystem(spy);

        var entity = new NullEntity();
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

    [Fact]
    public void Entity_marked_for_removal_is_removed_after_Tick_components_cleaned()
    {
        var core = new Core { Renderer = new NullRenderer() };
        var spy = new StorageSpySystem();
        core.AddSimSystem(spy);

        var entity = new NullEntity();
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

    [Fact]
    public void AddAllEntities_and_AddAllComponents_appear_after_Tick()
    {
        var core = new Core { Renderer = new NullRenderer() };
        var spy = new StorageSpySystem();
        core.AddSimSystem(spy);

        var e1 = new NullEntity();
        var e2 = new NullEntity();
        core.AddAllEntities([e1, e2]);
        core.AddAllComponents([
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
}
