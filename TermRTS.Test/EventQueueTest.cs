namespace TermRTS.Test;

public class EventQueueTest
{
    [Fact]
    public void TestEmpty()
    {
        var eq = new EventQueue<string, int>();
        Assert.Equal(0, eq.Count());
    }

    [Fact]
    public void TestFilled()
    {
        var eq = new EventQueue<string, int>();
        eq.TryAdd(("foo", 0));
        Assert.Equal(1, eq.Count());

        eq.TryAdd(("bar", 1));
        eq.TryAdd(("baz", 2));
        Assert.Equal(3, eq.Count());
    }

    [Fact]
    public void TestPriorities()
    {
        var eq = new EventQueue<string, int>();
        eq.TryAdd(("bar", 1));
        eq.TryAdd(("baz", 2));
        eq.TryAdd(("foo", 0));

        eq.TryPeek(out var elem1, out _);
        Assert.Equal("foo", elem1);
        eq.TryTake(out var a);
        Assert.Equal("foo", a.Item1);

        eq.TryPeek(out var elem2, out _);
        Assert.Equal("bar", elem2);
        eq.TryTake(out var b);
        Assert.Equal("bar", b.Item1);

        eq.TryPeek(out var elem3, out _);
        Assert.Equal("baz", elem3);
        eq.TryTake(out var c);
        Assert.Equal("baz", c.Item1);

        Assert.Equal(0, eq.Count());
    }
}
