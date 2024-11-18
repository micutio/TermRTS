using System.Collections;
using TermRTS.Examples.Greenery.Command;

namespace TermRTS.Test;

public class ScannerTest
{
    [Fact]
    public void TestEmptySource()
    {
        var scanner = new Scanner([]);
        Assert.Empty(scanner.ScanTokens());
    }
    
    [Theory]
    [ClassData(typeof(CharDataGenerator))]
    public void TestSingleToken((char[], Token) value)
    {
        var scanner = new Scanner(value.Item1);
        Assert.True(scanner.ScanTokens()[0].IsEqual(value.Item2));
    }
    
    [Theory]
    [ClassData(typeof(LiteralDataGenerator))]
    public void TestLiteralToken((char[], Token) value)
    {
        var scanner = new Scanner(value.Item1);
        Assert.True(scanner.ScanTokens()[0].IsEqual(value.Item2));
    }
}

/// <summary>
///     Extension method for comparing tokens.
/// </summary>
internal static class TokenExtension
{
    internal static bool IsEqual(this Token token, Token other)
    {
        var isEqual = token.TokenType.Equals(other.TokenType)
                      && token.Lexeme.Equals(other.Lexeme)
                      && Equals(token.Literal, other.Literal);
        
        if (!isEqual)
            Console.WriteLine($"ERROR, Mismatching Tokens ("
                              + $"{token.TokenType}, {token.Lexeme}, {token.Literal}) "
                              + $"!= ({other.TokenType}, {other.Lexeme}, {other.Literal})");
        
        return isEqual;
    }
}

public class CharDataGenerator : IEnumerable<object[]>
{
    private readonly List<object[]> _data =
    [
        [(new[] { '.' }, new Token(TokenType.Dot, ".", null))],
        [(new[] { '-' }, new Token(TokenType.Minus, "-", null))],
        [(new[] { '+' }, new Token(TokenType.Plus, "+", null))],
        [(new[] { '*' }, new Token(TokenType.Star, "*", null))],
        [(new[] { '!' }, new Token(TokenType.Bang, "!", null))],
        [(new[] { '!', '=' }, new Token(TokenType.BangEqual, "!=", null))]
    ];
    
    public IEnumerator<object[]> GetEnumerator()
    {
        return _data.GetEnumerator();
    }
    
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

public class LiteralDataGenerator : IEnumerable<object[]>
{
    private readonly List<object[]> _data =
    [
        [("Foo".ToCharArray(), new Token(TokenType.Identifier, "Foo", null))],
        [("Bar".ToCharArray(), new Token(TokenType.Identifier, "Bar", null))],
        [("\"Baz\"".ToCharArray(), new Token(TokenType.String, "\"Baz\"", "Baz"))],
        [("1234".ToCharArray(), new Token(TokenType.Number, "1234", 1234d))],
        [("1234.567".ToCharArray(), new Token(TokenType.Number, "1234.567", 1234.567d))],
        [("\"ImA1337Coder\"".ToCharArray(), new Token(TokenType.String, "\"ImA1337Coder\"", "ImA1337Coder"))],
        [("\"WTF".ToCharArray(), new Token(TokenType.UnfinishedString, "\"WTF", null))]
    ];
    
    public IEnumerator<object[]> GetEnumerator()
    {
        return _data.GetEnumerator();
    }
    
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}