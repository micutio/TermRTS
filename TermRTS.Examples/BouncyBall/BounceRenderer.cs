using System.Numerics;

namespace TermRTS.Examples.BouncyBall;

internal class BounceRenderer : TermRTS.IRenderer<BounceWorld, BounceComponents>
{
    public void RenderEntity(Dictionary<BounceComponents, IComponent> entity, double howFarIntoNextFrameMs)
    {
        if (!entity.TryGetValue(BounceComponents.Ball, out var ballComponent))
            return;

        var ball = (BounceBall)ballComponent;

        if (ball.Velocity == Vector2.Zero)
            return;

        Console.WriteLine($"ball pos {ball.Position}, velocity {ball.Velocity})");
    }

    public void RenderWorld(BounceWorld world, double howFarIntoNextFrameMs) { }
}
