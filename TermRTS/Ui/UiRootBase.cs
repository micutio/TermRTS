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

    public override void UpdateFromComponents(
        in IStorage componentStorage,
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

            foreach (var uiElement in _uiElements) uiElement.Render();
        }
        else
        {
            foreach (var uiElement in _uiElements)
                if (uiElement.IsRequireReRender)
                {
                    uiElement.Render();
                    uiElement.IsRequireReRender = false;
                    uiElement.IsRequireRootReRender = false;
                }
        }
    }

    #endregion

    #region Members

    protected abstract void UpdateThisFromComponents(
        in IStorage componentStorage,
        double timeStepSizeMs,
        double howFarIntoNextFramePercent);

    /// <summary>
    ///     Update this UiRoot from the components it depends on.
    ///     This <b>should</b> set <see cref="UiElementBase{TCanvas}.IsRequireReRender" /> and
    ///     <see cref="UiElementBase{TCanvas}.IsRequireRootReRender" />
    /// </summary>
    /// <param name="canvas"></param>
    protected abstract void RenderUiBase();

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