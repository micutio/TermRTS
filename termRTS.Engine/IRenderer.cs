namespace termRTS.Engine;

public interface IRenderer<in TWorld, TComponents> where TComponents : Enum
{

    public void RenderWorld(TWorld world, double howFarIntoNextFrameMs);

    public void RenderEntity(Dictionary<TComponents, IGameComponent> entity, double howFarIntoNextFrameMs);

}
