namespace TermRTS.Ui;

public readonly ref struct Rect(int x, int y, int width, int height)
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public int Width { get; } = width;
    public int Height { get; } = height;
}

public readonly ref struct RenderContext(ICanvas canvas, Rect bounds)
{
    public Rect Bounds { get; } = bounds;

    public void Draw(
        int localX,
        int localY,
        char character,
        ConsoleColor fgColor,
        ConsoleColor bgColor)
    {
        // Bounds check for clipping
        if (localX < 0 || localX >= Bounds.Width || localY < 0 || localY >= Bounds.Height)
            return;

        var globalX = Bounds.X + localX;
        var globalY = Bounds.Y + localY;

        canvas.SetCell(globalX, globalY, character, fgColor, bgColor);
    }

    public void Text(
        int localX,
        int localY,
        string text,
        bool isCentered,
        ConsoleColor fgColor,
        ConsoleColor bgColor)
    {
        // Bounds check for clipping
        if (localX < 0 || localX >= Bounds.Width || localY < 0 || localY >= Bounds.Height)
            return;

        var globalX = Bounds.X + localX;
        var globalY = Bounds.Y + localY;

        canvas.SetText(globalX, globalY, text, isCentered, fgColor, bgColor);
    }

    public RenderContext CreateSubContext(int localX, int localY, int width, int height)
    {
        var childBounds = new Rect(Bounds.X + localX, Bounds.Y + localY, width, height);
        return new RenderContext(canvas, childBounds);
    }
}