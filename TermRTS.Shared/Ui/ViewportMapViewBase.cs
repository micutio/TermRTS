using ConsoleRenderer;
using TermRTS.Io;
using TermRTS.Ui;

namespace TermRTS.Shared.Ui;

/// <summary>
///     Shared viewport, panning, and coordinate-scale rendering for top-down world map UIs.
/// </summary>
public abstract class ViewportMapViewBase : KeyInputProcessorBase
{
    protected const int SpaceForScaleTop = 1;
    protected const int SpaceForTextfieldBottom = 1;

    /// <summary>Default colors for the Greenery-style coordinate ruler (whitespace ticks).</summary>
    protected static readonly TerminalColor CoordinateDefaultBg = new(ConsoleColor.Black);

    protected static readonly TerminalColor CoordinateDefaultFg = new(ConsoleColor.Gray);

    protected readonly int WorldWidth;
    protected readonly int WorldHeight;
    protected readonly ConsoleCanvas Canvas;

    private int _spaceForScaleLeft;
    private int _viewportPositionInWorldX;
    private int _viewportPositionInWorldY;

    protected ViewportMapViewBase(ConsoleCanvas canvas, int worldWidth, int worldHeight)
    {
        Canvas = canvas;
        Canvas.AutoResize = true;
        WorldWidth = worldWidth;
        WorldHeight = worldHeight;
        ViewportPositionInWorldX = 0;
        ViewportPositionInWorldY = 0;
        Console.CursorVisible = false;
    }

    protected int ViewportWidth => Width - _spaceForScaleLeft;

    protected int ViewportHeight => Height - SpaceForScaleTop - SpaceForTextfieldBottom;

    protected int ViewportPositionInWorldX
    {
        get => _viewportPositionInWorldX;
        set
        {
            if (_viewportPositionInWorldX == value) return;

            _viewportPositionInWorldX = value;
            IsRequireReRender = true;
        }
    }

    protected int ViewportPositionInWorldY
    {
        get => _viewportPositionInWorldY;
        set
        {
            if (_viewportPositionInWorldY == value) return;

            _viewportPositionInWorldY = value;
            UpdateSpaceForScaleLeft();
            IsRequireReRender = true;
        }
    }

    protected int SpaceForScaleLeftValue => _spaceForScaleLeft;

    public override void HandleKeyInput(in ConsoleKeyInfo keyInfo)
    {
        switch (keyInfo.Key)
        {
            case ConsoleKey.UpArrow:
                MoveCameraUp();
                return;
            case ConsoleKey.DownArrow:
                MoveCameraDown();
                return;
            case ConsoleKey.LeftArrow:
                MoveCameraLeft();
                return;
            case ConsoleKey.RightArrow:
                MoveCameraRight();
                return;
            default:
                return;
        }
    }

    protected override void OnXChanged()
    {
        IsRequireReRender = true;
        IsRequireRootReRender = true;
    }

    protected override void OnYChanged()
    {
        IsRequireReRender = true;
        IsRequireRootReRender = true;
    }

    protected override void OnWidthChanged()
    {
        IsRequireReRender = true;
        IsRequireRootReRender = true;
    }

    protected override void OnHeightChanged()
    {
        UpdateSpaceForScaleLeft();
        IsRequireReRender = true;
        IsRequireRootReRender = true;
    }

    /// <summary>Greenery-style coordinate scales (whitespace tick marks).</summary>
    protected virtual void RenderCoordinates()
    {
        for (var x = 0; x < _spaceForScaleLeft; x++)
            Canvas.Set(X + x, Y, Cp437.WhiteSpace, CoordinateDefaultFg, CoordinateDefaultBg);

        for (var x = 0; x <= ViewportWidth; x++)
        {
            var worldX = ViewportToWorldX(x);
            var isTick = worldX > 0 && worldX % 10 == 0;
            var bg = isTick ? CoordinateDefaultFg : CoordinateDefaultBg;
            Canvas.Set(X + _spaceForScaleLeft + x, Y, Cp437.WhiteSpace, CoordinateDefaultFg, bg);
        }

        for (var x = 0; x <= ViewportWidth; x++)
        {
            var worldX = ViewportToWorldX(x);
            var isTick = worldX > 0 && worldX % 10 == 0;

            if (!isTick) continue;

            var spaceForLabel = Width - x - _spaceForScaleLeft;
            var tickLabel = Convert.ToString(worldX);
            if (tickLabel.Length > spaceForLabel) tickLabel = tickLabel[..spaceForLabel];
            Canvas.Text(X + _spaceForScaleLeft + x, Y, tickLabel, false, CoordinateDefaultBg,
                CoordinateDefaultFg);
        }

        for (var y = 0; y <= ViewportHeight; y++)
        for (var x = 0; x < _spaceForScaleLeft; x++)
        {
            var worldY = ViewportToWorldY(y);
            var isTick = worldY > 0 && worldY % 5 == 0;
            var bg = isTick ? CoordinateDefaultFg : CoordinateDefaultBg;
            Canvas.Set(X + x, y + SpaceForScaleTop, Cp437.WhiteSpace, CoordinateDefaultFg, bg);
        }

        for (var y = 0; y <= ViewportHeight; y++)
        {
            var worldY = ViewportToWorldY(y);
            var isTick = worldY > 0 && worldY % 5 == 0;
            if (isTick)
                Canvas.Text(X, y + SpaceForScaleTop, Convert.ToString(worldY), false,
                    CoordinateDefaultBg,
                    CoordinateDefaultFg);
        }
    }

    protected void UpdateSpaceForScaleLeft()
    {
        _spaceForScaleLeft = (ViewportPositionInWorldY + ViewportHeight) switch
        {
            < 10 => 1,
            < 100 => 2,
            < 1000 => 3,
            < 10000 => 4,
            _ => 5
        };
    }

    protected void MoveCameraUp()
    {
        ViewportPositionInWorldY = ViewportHeight > WorldHeight
            ? 0
            : Math.Max(ViewportPositionInWorldY - 1, 0);
    }

    protected void MoveCameraDown()
    {
        var maxViewportY = ViewportPositionInWorldY + ViewportHeight - 1;
        var maxWorldY = WorldHeight - ViewportHeight - 1;
        var boundaryY = Math.Min(maxViewportY, maxWorldY);
        ViewportPositionInWorldY = ViewportHeight > WorldHeight
            ? 0
            : Math.Min(ViewportPositionInWorldY + 1, boundaryY);
    }

    protected void MoveCameraLeft()
    {
        ViewportPositionInWorldX = ViewportWidth > WorldWidth
            ? 0
            : Math.Max(ViewportPositionInWorldX - 1, 0);
    }

    protected void MoveCameraRight()
    {
        var maxViewportX = ViewportPositionInWorldX + ViewportWidth - 1;
        var maxWorldX = WorldWidth - ViewportWidth - 1;
        var boundaryX = Math.Min(maxViewportX, maxWorldX);
        ViewportPositionInWorldX = ViewportWidth > WorldWidth
            ? 0
            : Math.Min(ViewportPositionInWorldX + 1, boundaryX);
    }

    /// <summary>Whether a world position is inside the current viewport.</summary>
    protected bool IsInCamera(float x, float y)
    {
        return x >= ViewportPositionInWorldX
               && x < ViewportPositionInWorldX + ViewportWidth
               && y >= ViewportPositionInWorldY
               && y < ViewportPositionInWorldY + ViewportHeight;
    }

    /// <summary>Whether a world position is inside the world grid.</summary>
    protected bool IsInBounds(float x, float y)
    {
        return x >= 0
               && x < WorldWidth
               && y >= 0
               && y < WorldHeight;
    }

    protected int WorldToViewportX(float x)
    {
        return Convert.ToInt32(x - ViewportPositionInWorldX);
    }

    protected int WorldToViewportY(float y)
    {
        return Convert.ToInt32(y - ViewportPositionInWorldY);
    }

    protected int ViewportToWorldX(int x)
    {
        return ViewportPositionInWorldX + x;
    }

    protected int ViewportToWorldY(int y)
    {
        return ViewportPositionInWorldY + y;
    }
}
