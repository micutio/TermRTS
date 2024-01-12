namespace termRTS.Test;

public class EventQueueTest
{
    [Fact]
    public void TestEmpty()
    {
        var eq = new termRTS.EventQueue<string, int>();
        Assert.Equal(0, eq.Count());
    }

    [Fact]
    public void TestFilled()
    {
        var eq = new termRTS.EventQueue<string, int>();
        eq.TryAdd(("foo", 0));
        Assert.Equal(1, eq.Count());

        eq.TryAdd(("bar", 1));
        eq.TryAdd(("baz", 2));
        Assert.Equal(3, eq.Count());
    }

    [Fact]
    public void TestPriorities()
    {
        var eq = new termRTS.EventQueue<string, int>();
        eq.TryAdd(("foo", 0));
        eq.TryAdd(("bar", 1));
        eq.TryAdd(("baz", 2));

        eq.TryPeek(out var elem1, out var prio1);
        Assert.Equal("foo", elem1);
        eq.TryTake(out var a);
        Assert.Equal("foo", a.Item1);

        eq.TryPeek(out var elem2, out var prio2);
        Assert.Equal("bar", elem2);
        eq.TryTake(out var b);
        Assert.Equal("bar", b.Item1);

        eq.TryPeek(out var elem3, out var prio3);
        Assert.Equal("baz", elem3);
        eq.TryTake(out var c);
        Assert.Equal("baz", c.Item1);

        Assert.Equal(0, eq.Count());
    }
}
