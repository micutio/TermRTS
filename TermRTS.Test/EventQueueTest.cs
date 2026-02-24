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

    [Fact]
    public void TryTakeIf_EmptyQueue_ReturnsFalse()
    {
        var eq = new EventQueue<string, int>();
        var taken = eq.TryTakeIf(_ => true, out var item);
        Assert.False(taken);
        Assert.Equal(default, item);
#pragma warning disable xUnit2013
        Assert.Equal(0, eq.Count);
#pragma warning restore xUnit2013
    }

    [Fact]
    public void TryTakeIf_FrontNotDue_ReturnsFalseAndLeavesQueueUnchanged()
    {
        var eq = new EventQueue<string, int>();
        eq.TryAdd(("first", 10));
        eq.TryAdd(("second", 20));

        var taken = eq.TryTakeIf(priority => priority <= 5, out var item);
        Assert.False(taken);
        Assert.Equal(default, item);
#pragma warning disable xUnit2013
        Assert.Equal(2, eq.Count);
#pragma warning restore xUnit2013

        eq.TryTake(out var front);
        Assert.Equal("first", front.Item1);
        Assert.Equal(10, front.Item2);
    }

    [Fact]
    public void TryTakeIf_FrontDue_DequeuesAndReturnsItem()
    {
        var eq = new EventQueue<string, int>();
        eq.TryAdd(("foo", 3));
        eq.TryAdd(("bar", 7));

        var taken = eq.TryTakeIf(priority => priority <= 5, out var item);
        Assert.True(taken);
        Assert.Equal("foo", item.Item1);
        Assert.Equal(3, item.Item2);
#pragma warning disable xUnit2013
        Assert.Equal(1, eq.Count);
#pragma warning restore xUnit2013

        eq.TryPeek(out var elem, out _);
        Assert.Equal("bar", elem);
    }

    [Fact]
    public void TryTakeIf_DrainsMultipleDueItemsInPriorityOrder()
    {
        var eq = new EventQueue<string, int>();
        eq.TryAdd(("late", 100));
        eq.TryAdd(("a", 1));
        eq.TryAdd(("b", 2));
        eq.TryAdd(("c", 3));

        var due = new List<string>();
        while (eq.TryTakeIf(priority => priority <= 10, out var item))
            due.Add(item.Item1);

        Assert.Equal(["a", "b", "c"], due);
#pragma warning disable xUnit2013
        Assert.Equal(1, eq.Count);
#pragma warning restore xUnit2013
        eq.TryTake(out var remaining);
        Assert.Equal("late", remaining.Item1);
        Assert.Equal(100, remaining.Item2);
    }
}