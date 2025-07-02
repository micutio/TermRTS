namespace TermRTS.Test;

public class SerializationTest
{
    [Fact]
    public void TestSchedulerSerialization()
    {
        var scheduler = new Scheduler(new Core
        {
            Renderer = new NullRenderer()
        });
        var persistence = new Persistence();

        var putSuccess1 =
            persistence.PutSimStateToJson(ref scheduler, out var expectedJsonStr, out _);

        Assert.True(putSuccess1);
        Assert.NotNull(expectedJsonStr);

        persistence.GetSimStateFromJson(ref scheduler, expectedJsonStr, out _);

        var putSuccess2 =
            persistence.PutSimStateToJson(ref scheduler, out var actualJsonStr, out _);

        Assert.True(putSuccess2);
        Assert.NotNull(actualJsonStr);
        Assert.Equal(expectedJsonStr, actualJsonStr);
    }
}