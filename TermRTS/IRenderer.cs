namespace TermRTS;

// TODO: Create a method for setup operations, before the simulation is run
public interface IRenderer
{
    /// <summary>
    ///     Called for each entity in the engine, at the end of each simulation tick
    /// </summary>
    /// <param name="entity">The entity to render</param>
    /// <param name="howFarIntoNextFramePercent">
    ///     How much extra time was needed for the last simulation tick.
    /// </param>
    //public void RenderEntity(
    //    Dictionary<Type, ComponentBase> entity,
    //    double howFarIntoNextFrameMs);
    public void RenderComponents(in IStorage storage, double timeStepSizeMs, double howFarIntoNextFramePercent);
    
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