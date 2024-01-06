namespace termRTS.Engine;

interface IRenderer<TW, T> where T : Enum
{

    public void renderWorld(TW world, double howfarIntoNextFrameMs);

    public void renderEntity(GameEntity<T> entity, double howFarIntoNextFrameMs);

}
