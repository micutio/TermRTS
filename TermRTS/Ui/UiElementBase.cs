using System.Threading.Channels;

namespace TermRTS.Ui;

public abstract class KeyInputProcessorBase<TCanvas> : UiElementBase<TCanvas>
{
    #region Fields

    private static int _runningId;

    private readonly int id;

    internal readonly ChannelReader<int> FocusSignalReader;

    /// <summary>
    /// Send <c>true</c> to try and claim focus, <c>false</c> to yield it again.
    /// TODO: Do I really need a channel for this?
    /// </summary>
    private readonly Channel<int> _focusSignal;

    #endregion

    #region Constructor

    protected KeyInputProcessorBase()
    {
        id = Interlocked.Increment(ref _runningId);
        _focusSignal = Channel.CreateUnbounded<int>();
        FocusSignalReader = _focusSignal.Reader;
    }

    #endregion

    protected void ClaimFocus()
    {
        _focusSignal.Writer.TryWrite(id);
    }

    protected void YieldFocus()
    {
        _focusSignal.Writer.TryWrite(id);
    }

    public abstract void HandleKeyInput(ref ConsoleKeyInfo keyInfo);
}

public abstract class UiElementBase<TCanvas>
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

    public bool IsRequireReRender { get; set; } = true;

    public bool IsRequireRootReRender { get; set; } = true;

    #endregion

    #region Public Methods

    /// <summary>
    /// Update the UI element from the components it depends on.
    /// This decides whether this component needs to be re-rendered, i.e.: this should set
    /// <see cref="IsRequireReRender"/> and <see cref="IsRequireRootReRender"/>.
    ///
    /// The update is separated from rendering to allow for parallelisation.
    /// </summary>
    /// <param name="componentStorage"></param>
    public abstract void UpdateFromComponents(
        in IStorage componentStorage,
        double timeStepSizeMs,
        double howFarIntoNextFramePercent);

    public abstract void Render(in TCanvas canvas);

    #endregion

    #region Protected Methods

    protected abstract void OnXChanged(int newX);

    protected abstract void OnYChanged(int newY);

    protected abstract void OnWidthChanged(int newWidth);

    protected abstract void OnHeightChanged(int newHeight);

    #endregion
}