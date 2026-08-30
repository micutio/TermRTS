using ConsoleRenderer;
using TermRTS.Ui;

namespace TermRTS.Examples.Greenery.Ui;

// TODO: NEVER EVER use Console.BackgroundColor and Console.ForegroundColor!
//       Because they are undefined in Linux and will crash ConsoleRenderer.

public class CanvasAdapter(ConsoleCanvas mainCanvas) : ICanvas
{
    public void SetCell(int x, int y, char character, ConsoleColor fgColor, ConsoleColor bgColor)
    {
        mainCanvas.Set(x, y, character, fgColor, bgColor);
    }

    public void SetText(
        int x,
        int y,
        string text,
        bool isCentered,
        ConsoleColor fgColor,
        ConsoleColor bgColor)
    {
        mainCanvas.Text(x, y, text, isCentered, fgColor, bgColor);
    }
}