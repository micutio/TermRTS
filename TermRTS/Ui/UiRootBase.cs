namespace TermRTS.Ui;

public abstract class UiRootBase<TCanvas> : UiElementBase<TCanvas>, IRenderer
{
    private readonly List<UiElementBase<TCanvas>> _uiElements = [];


    #region IRenderer Members

    public abstract void RenderComponents(in IStorage storage, double timeStepSizeMs,
        double howFarIntoNextFramePercent);

    public abstract void FinalizeRender();

    public abstract void Shutdown();

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
    /// <param name="canvas"></param>
    public override void Render(ref TCanvas canvas)
    {
        var isRequireReRender = IsRequireReRender
                                || _uiElements.Any(x => x.IsRequireRootReRender);
        if (isRequireReRender)
        {
            RenderUiBase(ref canvas);

            foreach (var uiElement in _uiElements) uiElement.Render(ref canvas);
        }
        else
        {
            foreach (var uiElement in _uiElements)
                if (uiElement.IsRequireReRender)
                {
                    uiElement.Render(ref canvas);
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
    protected abstract void RenderUiBase(ref TCanvas canvas);

    public void AddUiElement(UiElementBase<TCanvas> uiElement)
    {
        _uiElements.Add(uiElement);
    }

    public void RemoveUiElement(UiElementBase<TCanvas> uiElement)
    {
        _uiElements.Remove(uiElement);
    }

    #endregion
}