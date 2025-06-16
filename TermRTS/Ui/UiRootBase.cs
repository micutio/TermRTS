namespace TermRTS.Ui;

public abstract class UiRootBase : UiElementBase
{
    // TODO: Possibly create a stack to keep track of focused elements.
    //       If one of the child elements claims focus to this, then this claims focus to its
    //       parent and so on.

    #region Fields

    private readonly List<UiElementBase> _uiElements = [];

    #endregion

    #region UIElementBase Members

    // TODO: Find better method names to distinguish between UiRoot and UiElement members.
    public override void UpdateFromComponents(
        in IReadonlyStorage componentStorage,
        double timeStepSizeMs,
        double howFarIntoNextFramePercent)
    {
        IsRequireReRender = false;
        IsRequireRootReRender = false;

        UpdateThisFromComponents(componentStorage, timeStepSizeMs, howFarIntoNextFramePercent);

        foreach (var uiElement in _uiElements)
        {
            uiElement
                .UpdateFromComponents(componentStorage, timeStepSizeMs, howFarIntoNextFramePercent);
            IsRequireReRender &= uiElement.IsRequireRootReRender;
        }
    }

    /// <summary>
    ///     Render the root and all of its elements.
    ///     Only triggered if either the root
    /// </summary>
    public override void Render()
    {
        var isRequireReRender = IsRequireReRender
                                || _uiElements.Any(x => x.IsRequireRootReRender);

        if (isRequireReRender)
        {
            RenderUiBase();
            IsRequireReRender = false;
            IsRequireRootReRender = false;
        }

        foreach (var uiElement in _uiElements)
            if (uiElement.IsRequireReRender)
            {
                uiElement.Render();
                uiElement.IsRequireReRender = false;
                uiElement.IsRequireRootReRender = false;
            }
    }

    #endregion

    #region Abstract Members

    protected abstract void UpdateThisFromComponents(
        in IReadonlyStorage componentStorage,
        double timeStepSizeMs,
        double howFarIntoNextFramePercent);

    /// <summary>
    ///     Update this UiRoot from the components it depends on.
    ///     This <b>should</b> set <see cref="UiElementBase.IsRequireReRender" /> and
    ///     <see cref="UiElementBase.IsRequireRootReRender" />
    /// </summary>
    protected abstract void RenderUiBase();

    #endregion

    #region Public Members

    public void AddUiElement(UiElementBase uiElement)
    {
        _uiElements.Add(uiElement);
    }

    // TODO: Should this be allowed? Will require much bookkeeping
    public void RemoveUiElement(UiElementBase uiElement)
    {
        _uiElements.Remove(uiElement);
    }

    #endregion
}