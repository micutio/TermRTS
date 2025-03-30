namespace TermRTS.Test;

public class SerializationTest
{
    [Fact]
    public void TestSchedulerSerialization()
    {
        var sim = new Simulation(new Scheduler(new Core(new NullRenderer())));

        var expectedJsonStr = sim.SerializeSimulationStateToJson();
        Assert.NotNull(expectedJsonStr);

        sim.LoadSimulationStateFromJson(expectedJsonStr);

        var actualJsonStr = sim.SerializeSimulationStateToJson();
        Assert.NotNull(actualJsonStr);

        Assert.Equal(expectedJsonStr, actualJsonStr);
    }
}