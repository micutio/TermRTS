namespace TermRTS.Ui;

/// <summary>
///     Abstraction for a class that can write to a TUI canvas.
/// </summary>
public interface ICanvas
{
    void setCell(int x, int y, char character, ConsoleColor fgColor, ConsoleColor bgColor);
}