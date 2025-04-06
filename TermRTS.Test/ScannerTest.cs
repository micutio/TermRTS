using System.Collections;
using TermRTS.Algorithms;

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
    public void TestLiteralToken((string, Token) value)
    {
        var scanner = new Scanner(value.Item1.ToCharArray());
        Assert.True(scanner.ScanTokens()[0].IsEqual(value.Item2));
    }

    [Theory]
    [ClassData(typeof(TokenListDataGenerator))]
    public void TestTokenList((string, List<Token> tokenList) value)
    {
        var scanner = new Scanner(value.Item1.ToCharArray());
        var tokens = scanner.ScanTokens();
        Assert.Equal(tokens.Count, value.Item2.Count);
        for (var i = 0; i < tokens.Count; i++) Assert.True(tokens[i].Equals(value.Item2[i]));
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

    #region IEnumerable<object[]> Members

    public IEnumerator<object[]> GetEnumerator()
    {
        return _data.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    #endregion
}

public class LiteralDataGenerator : IEnumerable<object[]>
{
    private readonly List<object[]> _data =
    [
        [("Foo", new Token(TokenType.Identifier, "Foo", null))],
        [("Bar", new Token(TokenType.Identifier, "Bar", null))],
        [("\"Baz\"", new Token(TokenType.String, "\"Baz\"", "Baz"))],
        [("1234", new Token(TokenType.Number, "1234", 1234d))],
        [("1234.567", new Token(TokenType.Number, "1234.567", 1234.567d))],
        [("\"ImA1337Coder\"", new Token(TokenType.String, "\"ImA1337Coder\"", "ImA1337Coder"))],
        [("\"WTF", new Token(TokenType.UnfinishedString, "\"WTF", null))]
    ];

    #region IEnumerable<object[]> Members

    public IEnumerator<object[]> GetEnumerator()
    {
        return _data.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    #endregion
}

public class TokenListDataGenerator : IEnumerable<object[]>
{
    private readonly List<object[]> _data =
    [
        [
            ("Foo Bar Baz",
                (List<Token>)
                [
                    new Token(TokenType.Identifier, "Foo", null),
                    new Token(TokenType.Identifier, "Bar", null),
                    new Token(TokenType.Identifier, "Baz", null)
                ]
            )
        ],
        [
            ("Foo 123 4.567 \"Hornochse\"   ",
                (List<Token>)
                [
                    new Token(TokenType.Identifier, "Foo", null),
                    new Token(TokenType.Number, "123", 123d),
                    new Token(TokenType.Number, "4.567", 4.567d),
                    new Token(TokenType.String, "\"Hornochse\"", "Hornochse")
                ]
            )
        ]
    ];

    #region IEnumerable<object[]> Members

    public IEnumerator<object[]> GetEnumerator()
    {
        return _data.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    #endregion
}