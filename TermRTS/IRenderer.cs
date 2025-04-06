namespace TermRTS;

public interface IRenderer
{
    /// <summary>
    ///     Called for each entity in the engine, at the end of each simulation tick
    /// </summary>
    /// <param name="storage"> Instance of the component storage. </param>
    /// <param name="timeStepSizeMs"> Duration of one frame in Ms. </param>
    /// <param name="howFarIntoNextFramePercent">
    ///     How much extra time was needed for the last simulation tick.
    ///     Given in percent of <see cref="timeStepSizeMs" />.
    /// </param>
    public void RenderComponents(in IStorage storage, double timeStepSizeMs,
        double howFarIntoNextFramePercent);

    /// <summary>
    ///     Called at the end of the rendering step each simulation tick. This allows to apply the
    ///     effects of any intermediate operations.
    /// </summary>
    public void FinalizeRender();

    /// <summary>
    ///     Called upon engine shutdown. Allow the renderer to perform clean up operations.
    /// </summary>
    public void Shutdown();
}