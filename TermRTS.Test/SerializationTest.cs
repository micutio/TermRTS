namespace TermRTS.Test;

public class SerializationTest
{
    [Fact]
    public void TestSchedulerSerialization()
    {
        var scheduler = new Scheduler(new Core(new NullRenderer()));
        var persistence = new Persistence();

        var serializationError1 =
            persistence.SerializeSimulationStateToJson(ref scheduler, out var expectedJsonStr);

        Assert.Null(serializationError1);
        Assert.NotNull(expectedJsonStr);

        persistence.LoadSimulationStateFromJson(ref scheduler, expectedJsonStr);

        var serializationError2 =
            persistence.SerializeSimulationStateToJson(ref scheduler, out var actualJsonStr);

        Assert.Null(serializationError2);
        Assert.NotNull(actualJsonStr);
        Assert.Equal(expectedJsonStr, actualJsonStr);
    }
}