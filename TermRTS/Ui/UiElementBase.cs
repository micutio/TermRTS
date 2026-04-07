using TermRTS.Storage;

namespace TermRTS.Ui;

public abstract class UiElementBase
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
            _x = value;
            OnXChanged();
        }
    }

    public int Y
    {
        get => _y;
        set
        {
            _y = value;
            OnYChanged();
        }
    }

    public int Width
    {
        get => _width;
        set
        {
            _width = value;
            OnWidthChanged();
        }
    }

    public int Height
    {
        get => _height;
        set
        {
            _height = value;
            OnHeightChanged();
        }
    }

    public bool IsRequireReRender { get; set; } = true;

    public bool IsRequireRootReRender { get; set; } = true;

    #endregion

    #region Public Abstract Methods

    /// <summary>
    /// Update the UI element from the components it depends on.
    /// This decides whether this component needs to be re-rendered, i.e.: this should set
    /// <see cref="IsRequireReRender"/> and <see cref="IsRequireRootReRender"/>.
    /// 
    /// The update is separated from rendering to allow for parallelisation.
    /// </summary>
    /// <param name="componentStorage"></param>
    /// <param name="timeStepSizeMs"></param>
    /// <param name="howFarIntoNextFramePercent"></param>
    public abstract void UpdateFromComponents(
        in IReadonlyStorage componentStorage,
        double timeStepSizeMs,
        double howFarIntoNextFramePercent);

    public abstract void Render();

    #endregion

    #region Protected Abstract Methods

    protected abstract void OnXChanged();

    protected abstract void OnYChanged();

    protected abstract void OnWidthChanged();

    protected abstract void OnHeightChanged();

    #endregion
}