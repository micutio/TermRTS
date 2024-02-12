using System.Numerics;
using System.Threading.Channels;

namespace TermRTS.Examples.BouncyBall;

internal class BounceWorld : TermRTS.IWorld
{
    public void ApplyChange() { }
}

internal enum BounceComponents
{
    Ball,
}

internal class BounceBall : IComponent
{
    public Vector2 Position { get; set; }
    public Vector2 Velocity {get; set; }

    internal BounceBall(float x, float y, float dx, float dy)
    {
        Position = new Vector2(x, y);
        Velocity = new Vector2(dx, dy);
    }

    public object Clone()
    {
        return new BounceBall(Position.X, Position.Y, Velocity.X, Velocity.Y);
    }
}

internal class BounceEntity : EntityBase<BounceComponents> { }

// Bouncing ball and other physics:
//  - https://processing.org/examples/bouncingball.html
internal class BouncePhysicsSystem : System<BounceWorld, BounceComponents>, IEventSink
{
    private readonly Vector2 _minVelocity = new Vector2(0.1f, 0.1f);
    private Vector2 _velocity;

    public override Dictionary<BounceComponents, IComponent>? ProcessComponents(
            UInt64 timeStepSizeMs,
            EntityBase<BounceComponents> thisEntityComponents,
            List<EntityBase<BounceComponents>> otherEntityComponents,
            ref BounceWorld world)
    {
        if (!thisEntityComponents.Components.ContainsKey(BounceComponents.Ball))
            return new Dictionary<BounceComponents, IComponent>();

        var changedBallComponent = (BounceBall)thisEntityComponents.Components[BounceComponents.Ball];
        changedBallComponent.Velocity += _velocity;
        _velocity = Vector2.Zero;
        changedBallComponent.Position += changedBallComponent.Velocity;
        changedBallComponent.Velocity -= (changedBallComponent.Velocity * (0.45f * (timeStepSizeMs / 1000.0f)));
        Vector2.Clamp(changedBallComponent.Position, _minVelocity, changedBallComponent.Position);

        return new Dictionary<BounceComponents, IComponent> { { BounceComponents.Ball, changedBallComponent } };
    }

    public void ProcessEvent(IEvent evt)
    {
        if (evt.Type() == EventType.KeyInput)
        {
            var keyEvent = (KeyInputEvent)evt;
            switch (keyEvent.Info.Key)
            {
                case ConsoleKey.A:
                    _velocity.X -= 1;
                    break;
                case ConsoleKey.D:
                    _velocity.X += 1;
                    break;
                case ConsoleKey.W:
                    _velocity.Y -= 1;
                    break;
                case ConsoleKey.S:
                    _velocity.Y += 1;
                    break;
                default:
                    break;
            }
        }
    }
}

public class BounceApp : IRunnableExample
{
    public void Run()
    {
        var core = new Core<BounceWorld, BounceComponents>(new BounceWorld(), new BounceRenderer());
        var bouncePhysics = new BouncePhysicsSystem();
        core.AddGameSystem(bouncePhysics);
        var bounceEntity = new BounceEntity();
        bounceEntity.AddComponent(BounceComponents.Ball, new BounceBall(10f, 10f, 0f, 0f));
        core.AddEntity(bounceEntity);

        var scheduler = new Scheduler(16, 16, core);
        scheduler.AddEventSink(core, EventType.Shutdown);
        scheduler.AddEventSink(bouncePhysics, EventType.KeyInput);

        var input = new TermRTS.IO.ConsoleInput();
        scheduler.AddEventSources(input.KeyEventReader);
        input.Run();

        // Run it
        scheduler.SimulationLoop();

        // It should terminate after 12 ticks of 16ms simulated time each.
    }
}
