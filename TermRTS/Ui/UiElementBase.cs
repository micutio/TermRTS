using TermRTS.Storage;

namespace TermRTS.Ui;

public abstract class UiElementBase
{
    #region Fields

    private readonly List<UiElementBase> _uiElements = [];

    #endregion

    #region Properties

    public int X
    {
        get;
        set
        {
            field = value;
            OnXChanged();
        }
    }

    public int Y
    {
        get;
        set
        {
            field = value;
            OnYChanged();
        }
    }

    public int Width
    {
        get;
        set
        {
            field = value;
            OnWidthChanged();
        }
    }

    public int Height
    {
        get;
        set
        {
            field = value;
            OnHeightChanged();
        }
    }

    /// <summary>
    /// Flag indicating whether the contents of this UI element needs to be re-rendered.
    /// If the UI element changes it's layout, which would affect other elements of this
    /// element tree then use <see cref="IsRequireRootRender"/> instead.
    /// </summary>
    public bool IsRequireRender { get; set; } = true;

    /// <summary>
    /// Flag indicating that this UI element or any of its child elements has changed their layout.
    /// </summary>
    public bool IsRequireRootRender { get; set; } = true;

    #endregion

    #region Public Abstract Members

    /// <summary>
    /// Update the UI element from the components it depends on.
    /// This decides whether this component needs to be re-rendered, i.e.: this should set
    /// <see cref="IsRequireRender"/> and <see cref="IsRequireRootRender"/>.
    /// 
    /// The update is separated from rendering to allow for parallelisation.
    /// </summary>
    /// <param name="componentStorage"></param>
    /// <param name="timeStepSizeMs"></param>
    /// <param name="howFarIntoNextFramePercent"></param>
    public abstract void UpdateSelfFromComponents(
        in IReadonlyStorage componentStorage,
        double timeStepSizeMs,
        double howFarIntoNextFramePercent);

    public abstract void RenderSelf();

    protected abstract void OnXChanged();

    protected abstract void OnYChanged();

    protected abstract void OnWidthChanged();

    protected abstract void OnHeightChanged();

    #endregion

    #region Public Members

    public void UpdateUiTreeFromComponents(
        in IReadonlyStorage componentStorage,
        double timeStepSizeMs,
        double howFarIntoNextFramePercent)
    {
        // IsRequireRender = false;
        // IsRequireRootRender = false;

        UpdateSelfFromComponents(componentStorage, timeStepSizeMs, howFarIntoNextFramePercent);

        foreach (var uiElement in _uiElements)
        {
            uiElement
                .UpdateUiTreeFromComponents(
                    componentStorage,
                    timeStepSizeMs,
                    howFarIntoNextFramePercent);
            IsRequireRender &= uiElement.IsRequireRootRender;
        }
    }

    /// <summary>
    ///     Render the root and all of its elements.
    ///     Only triggered if either the root or one of its child UI-elements requires a re-render.
    /// </summary>
    public void RenderUiTree()
    {
        // TODO: Remove this after verifying that UpdateThisFromComponents already does this check.
        // var isRequireReRender = IsRequireReRender
        //                         || _uiElements.Any(x => x.IsRequireRootReRender);

        if (IsRequireRender)
        {
            RenderSelf();
            IsRequireRender = false;
            IsRequireRootRender = false;
        }

        foreach (var uiElement in _uiElements)
            if (uiElement.IsRequireRender)
            {
                uiElement.RenderUiTree();
            }
    }

    protected void AddChildUiElement(UiElementBase uiElement)
    {
        _uiElements.Add(uiElement);
    }

    public void RemoveChildUiElement(UiElementBase uiElement)
    {
        _uiElements.Remove(uiElement);
    }

    #endregion
}