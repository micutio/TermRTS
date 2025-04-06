namespace TermRTS.Test;

public class SerializationTest
{
    [Fact]
    public void TestSchedulerSerialization()
    {
        var scheduler = new Scheduler(new Core(new NullRenderer()));
        var persistence = new Persistence();

        var expectedJsonStr = persistence.SerializeSimulationStateToJson(ref scheduler);
        Assert.NotNull(expectedJsonStr);

        persistence.LoadSimulationStateFromJson(ref scheduler, expectedJsonStr);

        var actualJsonStr = persistence.SerializeSimulationStateToJson(ref scheduler);
        Assert.NotNull(actualJsonStr);

        Assert.Equal(expectedJsonStr, actualJsonStr);
    }
}