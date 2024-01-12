namespace termRTS.Test;

public class EventQueueTest
{
    [Fact]
    public void TestEmpty()
    {
        var eq = new termRTS.EventQueue<string, int>();
        Assert.True(eq.Count() == 0);
    }

    [Fact]
    public void TestFilled()
    {
        var eq = new termRTS.EventQueue<string, int>();
        eq.TryAdd(("foo", 0));
        Assert.True(eq.Count() == 1);

        eq.TryAdd(("bar", 1));
        eq.TryAdd(("baz", 2));
        Assert.True(eq.Count() == 3);
    }
}
