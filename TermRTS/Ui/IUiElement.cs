namespace TermRTS.Ui;

public abstract class IUiElement<TCanvas>
{
    #region Fields

    private int _x;
    private int _y;
    private int _width;
    private int _height;

    #endregion

    #region Properties

    public int X
    {
        get => _x;
        set
        {
            OnXChanged(value);
            _x = value;
        }
    }

    public int Y
    {
        get => _y;
        set
        {
            OnYChanged(value);
            _y = value;
        }
    }

    public int Width
    {
        get => _width;
        set
        {
            OnWidthChanged(value);
            _width = value;
        }
    }

    public int Height
    {
        get => _height;
        set
        {
            OnHeightChanged(value);
            _height = value;
        }
    }

    #endregion

    #region Public Methods

    public abstract void Render(ref TCanvas canvas);

    #endregion

    #region Protected Methods

    protected abstract void OnXChanged(int newX);

    protected abstract void OnYChanged(int newY);

    protected abstract void OnWidthChanged(int newWidth);

    protected abstract void OnHeightChanged(int newHeight);

    #endregion
}