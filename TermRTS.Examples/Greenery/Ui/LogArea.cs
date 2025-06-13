using ConsoleRenderer;
using TermRTS.Event;
using TermRTS.Ui;

namespace TermRTS.Examples.Greenery.Ui;

// TODO: Implementation Ideas
//       - Ringbuffer, containing lines of text
//       - Method for adding new text into buffer
//       - make it scrollable?
public class LogArea : UiElementBase, IEventSink
{
    #region Fields

    private readonly Queue<string> _logEntries = [];

    #endregion

    #region IEventSink Members

    public void ProcessEvent(IEvent evt)
    {
        if (evt is not Event<SystemLog>(var logContent)) return;

        _logEntries.Enqueue(logContent.Content);
        IsRequireReRender = true;
    }

    #endregion

    #region UiElementBase Members

    public override void UpdateFromComponents(
        in IReadonlyStorage componentStorage,
        double timeStepSizeMs,
        double howFarIntoNextFramePercent)
    {
        // Does not require components to work.
    }

    public override void Render()
    {
        // TODO:
        throw new NotImplementedException();
    }

    protected override void OnXChanged(int newX)
    {
        IsRequireReRender = true;
    }

    protected override void OnYChanged(int newY)
    {
        IsRequireReRender = true;
    }

    protected override void OnWidthChanged(int newWidth)
    {
        IsRequireReRender = true;
    }

    protected override void OnHeightChanged(int newHeight)
    {
        IsRequireReRender = true;
    }

    #endregion
}