namespace TermRTS;

// TODO: Create a method for setup operations, before the simulation is run
// TODO: Create a method for cleanup operations, i.e.: clear the canvas for terminal apps.
public interface IRenderer<in TWorld, TComponents> where TComponents : Enum
{

    /// <summary>
    /// Render the given world object, called every tick.
    /// </summary>
    /// <param name="world"> Instance of the world </param>
    /// <param name="timeStepSizeMs"> How many milliseconds are covered by one timestep </param>
    /// <param name="howFarIntoNextFrameMs">
    /// If the previous tick took longer than the allocated <paramref name="timeStepSizeMs"/>,
    /// then this indicates the 'spill over' into the next time step, to allow the renderer to
    /// account for it.
    /// </param>
    public void RenderWorld(TWorld world, double timeStepSizeMs, double howFarIntoNextFrameMs);

    public void RenderEntity(
            Dictionary<TComponents, IComponent> entity,
            double howFarIntoNextFrameMs);

    public void FinalizeRender();

    public void Shutdown();
}

