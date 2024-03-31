using System.Numerics;
using System.Threading.Channels;

namespace TermRTS.Examples.BouncyBall;

internal class BounceWorld : TermRTS.IWorld
{
    public void ApplyChange() { }
}

internal enum BounceComponentTypes
{
    Ball,
}

internal class BounceBall : IComponent
{
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }

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

internal class BounceEntity : EntityBase<BounceComponentTypes> { }

// Bouncing ball and other physics:
//  - https://processing.org/examples/bouncingball.html
internal class BouncePhysicsSystem : System<BounceWorld, BounceComponentTypes>, IEventSink
{
    private readonly Vector2 _minVelocity = new Vector2(0.1f, 0.1f);
    private Vector2 _velocity;

    public override Dictionary<BounceComponentTypes, IComponent>? ProcessComponents(
            UInt64 timeStepSizeMs,
            EntityBase<BounceComponentTypes> thisEntityComponents,
            List<EntityBase<BounceComponentTypes>> otherEntityComponents,
            ref BounceWorld world)
    {
        if (!thisEntityComponents.Components.ContainsKey(BounceComponentTypes.Ball))
            return new Dictionary<BounceComponentTypes, IComponent>();

        var maxX = Console.BufferWidth;
        var maxY = Console.BufferHeight;

        var timeRatio = timeStepSizeMs / 1000.0f;
        var changedBallComponent = (BounceBall)thisEntityComponents.Components[BounceComponentTypes.Ball];
        var ballVel = changedBallComponent.Velocity;
        var ballPos = changedBallComponent.Position;
        ballVel += _velocity;
        _velocity = Vector2.Zero;
        ballPos += ballVel;
        ballVel = Vector2.Multiply(ballVel, 0.90f);

        if (Math.Abs(ballVel.X) < 0.1f) ballVel.X = 0.0f;
        if (Math.Abs(ballVel.Y) < 0.1f) ballVel.Y = 0.0f;

        if (ballPos.X >= maxX)
        {
            ballPos.X = maxX - 1;
            ballVel.X = 0.0f;
        }

        if (ballPos.X <= 0)
        {
            ballPos.X = 0;
            ballVel.X = 0.0f;
        }

        if (ballPos.Y >= maxY)
        {
            ballPos.Y = maxY - 1;
            ballVel.Y = 0.0f;
        }

        if (ballPos.Y <= 0)
        {
            ballPos.Y = 0;
            ballVel.Y = 0.0f;
        }

        changedBallComponent.Position = ballPos;
        changedBallComponent.Velocity = ballVel;

        return new Dictionary<BounceComponentTypes, IComponent> { { BounceComponentTypes.Ball, changedBallComponent } };
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
        var core = new Core<BounceWorld, BounceComponentTypes>(new BounceWorld(), new BounceRenderer());
        var bouncePhysics = new BouncePhysicsSystem();
        core.AddGameSystem(bouncePhysics);
        var bounceEntity = new BounceEntity();
        bounceEntity.AddComponent(BounceComponentTypes.Ball, new BounceBall(10f, 10f, 0f, 0f));
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
