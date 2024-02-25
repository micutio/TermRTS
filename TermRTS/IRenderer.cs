namespace TermRTS;

public interface IRenderer<in TWorld, TComponents> where TComponents : Enum
{

    public void RenderWorld(TWorld world, double howFarIntoNextFrameMs);

    public void RenderEntity(
            Dictionary<TComponents, IComponent> entity,
            double howFarIntoNextFrameMs);

    public void FinalizeRender();

}
