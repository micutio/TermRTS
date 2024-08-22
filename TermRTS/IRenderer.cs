namespace TermRTS;

// TODO: Create a method for setup operations, before the simulation is run
public interface IRenderer<in TWorld>
{
    /// <summary>
    ///     Render the given world object, called every tick.
    /// </summary>
    /// <param name="world"> Instance of the world </param>
    /// <param name="timeStepSizeMs"> How many milliseconds are covered by one timestep </param>
    /// <param name="howFarIntoNextFrameMs">
    ///     If the previous tick took longer than the allocated <paramref name="timeStepSizeMs" />
    ///     then this indicates the 'spill over' into the next time step, to allow the renderer to
    ///     account for it.
    /// </param>
    public void RenderWorld(TWorld world, double timeStepSizeMs, double howFarIntoNextFrameMs);

    /// <summary>
    ///     Called for each entity in the engine, at the end of each simulation tick
    /// </summary>
    /// <param name="entity">The entity to render</param>
    /// <param name="howFarIntoNextFrameMs">
    ///     How much extra time was needed for the last simulation tick.
    /// </param>
    public void RenderEntity(
        Dictionary<Type, IComponent> entity,
        double howFarIntoNextFrameMs);

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