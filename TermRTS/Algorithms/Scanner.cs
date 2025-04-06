// https://craftinginterpreters.com/scanning.html

namespace TermRTS.Algorithms;

public enum TokenType
{
    // Single-character tokens
    Dot,
    Minus,
    Plus,
    Star,

    // One or two character tokens
    Bang,
    BangEqual,

    // Literals
    Identifier,
    String,
    Number,

    // Special
    UnfinishedString, // For live syntax highlighting
    Unknown
}

public readonly record struct Token(TokenType TokenType, string Lexeme, object? Literal)
{
    public override string ToString()
    {
        return TokenType + " " + Lexeme + " " + Literal;
    }
}

/// <summary>
///     Parses text to tokens.
/// </summary>
public class Scanner(char[] source)
{
    private readonly List<Token> _tokens = [];
    private int _current;
    private int _start;

    private bool IsAtEnd => _current >= source.Length;

    public IReadOnlyList<Token> ScanTokens()
    {
        while (!IsAtEnd)
        {
            _start = _current;
            ScanToken();
        }

        return _tokens;
    }

    private void ScanToken()
    {
        var c = Advance();
        switch (c)
        {
            case '-':
                AddToken(TokenType.Minus);
                break;
            case '+':
                AddToken(TokenType.Plus);
                break;
            case '*':
                AddToken(TokenType.Star);
                break;
            case '.':
                AddToken(TokenType.Dot);
                break;
            // For two-character long signs we need to look one char ahead
            case '!':
                AddToken(Match('=') ? TokenType.BangEqual : TokenType.Bang);
                break;
            // For longer tokens we need to look ahead until we find a terminator
            case '"':
                TakeString();
                break;
            // Skip whitespaces
            case ' ':
            case '\0':
            case '\t':
            case '\r':
                break;

            default: // TODO: How to handle erroneous input?
                if (IsDigit(c))
                    TakeNumber();
                else if (IsAlpha(c))
                    TakeIdentifier();
                else
                    AddToken(TokenType.Unknown);

                break;
        }
    }

    private char Advance()
    {
        return source[_current++];
    }

    private bool Match(char expected)
    {
        if (IsAtEnd) return false;
        if (source[_current] != expected) return false;

        _current++;
        return true;
    }

    private char Peek()
    {
        return IsAtEnd ? '\0' : source[_current];
    }

    private char PeekNext()
    {
        return _current + 1 >= source.Length ? '\0' : source[_current + 1];
    }

    /// <summary>
    ///     Consume as many digits as can be found for the integer part,
    ///     then look for a decimal point and a fractional part of more digits.
    /// </summary>
    /// <param name="c"></param>
    /// <returns></returns>
    private static bool IsDigit(char c)
    {
        return c is >= '0' and <= '9';
    }

    private static bool IsAlpha(char c)
    {
        return c is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or '_';
    }

    private static bool IsAlphaNumeric(char c)
    {
        return IsAlpha(c) || IsDigit(c);
    }

    private void TakeString()
    {
        while (Peek() != '"' && !IsAtEnd)
            Advance();

        if (IsAtEnd)
        {
            AddToken(TokenType.UnfinishedString);
            return;
        }

        // The closing "
        Advance();

        // Trim the surrounding quotes
        AddToken(TokenType.String, new string(source, _start + 1, _current - _start - 2));
    }

    private void TakeNumber()
    {
        while (IsDigit(Peek())) Advance();

        // Look for a fractional part
        if (Peek() == '.' && IsDigit(PeekNext()))
        {
            // Consume the "."
            Advance();

            while (IsDigit(Peek())) Advance();
        }

        AddToken(
            TokenType.Number,
            Convert.ToDouble(new string(source, _start, _current - _start)));
    }

    private void TakeIdentifier()
    {
        while (IsAlphaNumeric(Peek())) Advance();

        // Optional, check if map of keywords contains text
        // if so, token type = KEYWORD, otherwise IDENTIFIER.

        AddToken(TokenType.Identifier);
    }

    private void AddToken(TokenType tokenType, object? literal = null)
    {
        var lexeme = new string(source, _start, _current - _start);
        _tokens.Add(new Token(tokenType, lexeme, literal));
    }
}