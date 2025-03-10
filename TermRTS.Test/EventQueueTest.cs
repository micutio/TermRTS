using TermRTS.Event;

namespace TermRTS.Test;

public class EventQueueTest
{
    [Fact]
    public void TestEmpty()
    {
#pragma warning disable xUnit2013
        Assert.Equal(0, new EventQueue<string, int>().Count);
#pragma warning restore xUnit2013
        // Assert.Empty(new EventQueue<string, int>());
    }
    
    [Fact]
    public void TestFilled()
    {
        var eq = new EventQueue<string, int>();
        eq.TryAdd(("foo", 0));
        // Do not use Assert.Single(), because our PQ implementation doesn't support enumerators
#pragma warning disable xUnit2013
        Assert.Equal(1, eq.Count);
#pragma warning restore xUnit2013
        
        eq.TryAdd(("bar", 1));
        eq.TryAdd(("baz", 2));
        Assert.Equal(3, eq.Count);
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
        
#pragma warning disable xUnit2013
        Assert.Equal(0, eq.Count);
#pragma warning restore xUnit2013
    }
}