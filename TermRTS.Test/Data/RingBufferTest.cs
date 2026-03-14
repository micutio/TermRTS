using TermRTS.Data;

namespace TermRTS.Test.Data;

public class RingBufferTest
{
    [Fact]
    public void Constructor_zero_capacity_throws()
    {
        Assert.Throws<ArgumentException>(() => new RingBuffer<int>(0));
    }

    [Fact]
    public void Constructor_negative_capacity_throws()
    {
        Assert.Throws<ArgumentException>(() => new RingBuffer<int>(-1));
    }

    [Fact]
    public void Constructor_with_items_exceeds_capacity_throws()
    {
        Assert.Throws<ArgumentException>(() => new RingBuffer<int>(2, [1, 2, 3]));
    }

    [Fact]
    public void Constructor_with_items_sets_size_and_order()
    {
        var buf = new RingBuffer<int>(5, [10, 20, 30]);
        Assert.Equal(3, buf.Size);
        Assert.Equal(5, buf.Capacity);
        Assert.False(buf.IsFull);
        Assert.False(buf.IsEmpty);
        Assert.Equal(10, buf.Front());
        Assert.Equal(30, buf.Back());
        Assert.Equal(10, buf[0]);
        Assert.Equal(20, buf[1]);
        Assert.Equal(30, buf[2]);
    }

    [Fact]
    public void PushBack_increments_size_until_full()
    {
        var buf = new RingBuffer<int>(3);
        buf.PushBack(1);
        Assert.Equal(1, buf.Size);
        Assert.Equal(1, buf.Front());
        Assert.Equal(1, buf.Back());
        buf.PushBack(2);
        Assert.Equal(2, buf.Size);
        Assert.Equal(1, buf.Front());
        Assert.Equal(2, buf.Back());
        buf.PushBack(3);
        Assert.Equal(3, buf.Size);
        Assert.True(buf.IsFull);
        Assert.Equal(1, buf.Front());
        Assert.Equal(3, buf.Back());
    }

    [Fact]
    public void PushBack_when_full_overwrites_oldest()
    {
        var buf = new RingBuffer<int>(3);
        buf.PushBack(1);
        buf.PushBack(2);
        buf.PushBack(3);
        buf.PushBack(4);

        Assert.True(buf.IsFull);
        Assert.Equal(3, buf.Size);
        Assert.Equal(2, buf.Front());
        Assert.Equal(4, buf.Back());
        Assert.Equal(2, buf[0]);
        Assert.Equal(3, buf[1]);
        Assert.Equal(4, buf[2]);
    }

    [Fact]
    public void PopFront_removes_front_and_decrements_size()
    {
        var buf = new RingBuffer<int>(3, [10, 20, 30]);
        buf.PopFront();
        Assert.Equal(2, buf.Size);
        Assert.Equal(20, buf.Front());
        Assert.Equal(30, buf.Back());
        buf.PopFront();
        Assert.Equal(1, buf.Size);
        Assert.Equal(30, buf.Front());
        Assert.Equal(30, buf.Back());
        buf.PopFront();
        Assert.True(buf.IsEmpty);
    }

    [Fact]
    public void PopFront_empty_throws()
    {
        var buf = new RingBuffer<int>(2);
        Assert.Throws<InvalidOperationException>(() => buf.PopFront());
    }

    [Fact]
    public void PopBack_removes_back()
    {
        var buf = new RingBuffer<int>(3, [10, 20, 30]);
        buf.PopBack();
        Assert.Equal(2, buf.Size);
        Assert.Equal(10, buf.Front());
        Assert.Equal(20, buf.Back());
    }

    [Fact]
    public void PopBack_empty_throws()
    {
        var buf = new RingBuffer<int>(2);
        Assert.Throws<InvalidOperationException>(() => buf.PopBack());
    }

    [Fact]
    public void PushFront_when_full_overwrites_back()
    {
        var buf = new RingBuffer<int>(3);
        buf.PushBack(1);
        buf.PushBack(2);
        buf.PushBack(3);
        buf.PushFront(0);

        Assert.True(buf.IsFull);
        Assert.Equal(3, buf.Size);
        Assert.Equal(0, buf.Front());
        Assert.Equal(2, buf.Back());
    }

    [Fact]
    public void Clear_resets_size()
    {
        var buf = new RingBuffer<int>(3, [1, 2, 3]);
        buf.Clear();
        Assert.True(buf.IsEmpty);
        Assert.Equal(0, buf.Size);
        Assert.Equal(3, buf.Capacity);
        Assert.Throws<InvalidOperationException>(() => buf.Front());
    }

    [Fact]
    public void ToArray_returns_logical_order()
    {
        var buf = new RingBuffer<int>(4, [1, 2, 3]);
        buf.PushBack(4);
        var arr = buf.ToArray();
        Assert.Equal([1, 2, 3, 4], arr);
    }

    [Fact]
    public void Enumerator_iterates_in_logical_order()
    {
        var buf = new RingBuffer<int>(3, [7, 8, 9]);
        var list = buf.ToList();
        Assert.Equal([7, 8, 9], list);
    }
}
