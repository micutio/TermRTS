namespace TermRTS.Test;

public class SerializationTest
{
    [Fact]
    public void TestSchedularSerialization()
    {
        var scheduler = new Scheduler(new Core(new NullRenderer()));
        var sim = new Simulation(scheduler);
        var jsonStr = sim.SerializeSimulationStateToJson();
        Assert.NotNull(jsonStr);
        var roundTripScheduler = sim.LoadSimulationStateFromJson(jsonStr);
        Assert.NotNull(roundTripScheduler);
        Assert.Equal(scheduler, roundTripScheduler);
    }
}