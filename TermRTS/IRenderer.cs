namespace TermRTS;

// TODO: Create a method for cleanup operations, i.e.: clear the canvas for terminal apps.
public interface IRenderer<in TWorld, TComponents> where TComponents : Enum
{

    public void RenderWorld(TWorld world, double howFarIntoNextFrameMs);

    public void RenderEntity(
            Dictionary<TComponents, IComponent> entity,
            double howFarIntoNextFrameMs);

    public void FinalizeRender();

}

