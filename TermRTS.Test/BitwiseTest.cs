namespace TermRTS.Test;

public class BitwiseTest
{
    [Fact]
    public void BitwiseAnd()
    {
        var a = 1;
        var b = 2;
        var c = 0;
        c |= 1;
        Assert.Equal(a, c);
        c <<= 1;
        Assert.Equal(b, c);
    }
}