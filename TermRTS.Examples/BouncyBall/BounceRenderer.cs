namespace TermRTS.Examples.BouncyBall;

internal class BounceRenderer : TermRTS.IRenderer<BounceWorld, BounceComponents>
{
    public void RenderEntity(Dictionary<BounceComponents, IComponent> entity, double howFarIntoNextFrameMs)
    {
        if (!entity.TryGetValue(BounceComponents.Ball, out var ballComponent))
            return;

        var ball = (BounceBall)ballComponent;

        if (ball is { DeltaX: 0.0f, DeltaY: 0.0f })
            return;

        Console.WriteLine($"ball x {ball.X}, (dx {ball.DeltaX}), y {ball.Y} (dy {ball.DeltaY})");
    }

    public void RenderWorld(BounceWorld world, double howFarIntoNextFrameMs) { }
}
