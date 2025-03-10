namespace TermRTS.Test;

public class SerializationTest
{
    [Fact]
    public void TestSchedulerSerialization()
    {
        var expectedScheduler = new Scheduler(new Core(new NullRenderer()));
        var sim = new Simulation(expectedScheduler);
        
        var expectedJsonStr = sim.SerializeSimulationStateToJson();
        Assert.NotNull(expectedJsonStr);
        
        var actualScheduler = sim.LoadSimulationStateFromJson(expectedJsonStr);
        Assert.NotNull(actualScheduler);
        
        var actualJsonStr = sim.SerializeSimulationStateToJson();
        Assert.NotNull(actualJsonStr);
        
        Assert.Equal(expectedJsonStr, actualJsonStr);
    }
}