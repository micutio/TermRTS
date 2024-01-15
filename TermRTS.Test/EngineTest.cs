
namespace TermRTS.Test;

public class NullWorld : IWorld
{
    public void ApplyChange()
    {
        Console.WriteLine("World changed.");
    }
}

public enum EmptyComponentType
{
    Empty,
}

public class NullRenderer : IRenderer<NullWorld, EmptyComponentType>
{
    public void RenderEntity(
            Dictionary<EmptyComponentType, IComponent> entity,
            double howFarIntoNextFrameMs)
    {
        Console.WriteLine($"Rendering null-entity at {howFarIntoNextFrameMs} ms into next frame.");
    }

    public void RenderWorld(NullWorld world, double howFarIntoNextFrameMs)
    {
        Console.WriteLine($"Rendering null-world at {howFarIntoNextFrameMs} ms into next frame.");
    }
}

public class EngineTestTheoryData : TheoryData<Core<NullWorld, EmptyComponentType>>
{
    public EngineTestTheoryData()
    {
        Add(new Core<NullWorld, EmptyComponentType>(new NullWorld(), new NullRenderer()));
    }
}

public class WatcherSystem : System<NullWorld, EmptyComponentType>
{

    public override Dictionary<EmptyComponentType, IComponent>? ProcessComponents(UInt128 timeStepSize, EntityBase<EmptyComponentType> thisEntityComponents, List<EntityBase<EmptyComponentType>> otherEntityComponents, ref NullWorld world)
    {
        return null;
    }

}

public class EngineTest
{
    [Theory]
    [ClassData(typeof(EngineTestTheoryData))]
    public void TestSetup(Core<NullWorld, EmptyComponentType> core)
    {
        Assert.True(core.IsGameRunning());
        core.Tick(16L);
        core.Tick(16L);
        core.Tick(16L);
        core.Tick(16L);
        core.Shutdown();
        Assert.False(core.IsGameRunning());
    }

    [Theory]
    [ClassData(typeof(EngineTestTheoryData))]
    public void TestSchedulerSetup(Core<NullWorld, EmptyComponentType> core)
    {
        var scheduler = new Scheduler(16, 16, core);

    }
}
