namespace TermRTS.Ui;

/// <summary>
///     Abstraction for a class that can write to a TUI canvas.
/// </summary>
public interface ICanvas
{
    void SetCell(int x, int y, char character, ConsoleColor fgColor, ConsoleColor bgColor);

    void SetText(int x, int y, string text, bool isCentered, ConsoleColor fgColor,
        ConsoleColor bgColor);
}