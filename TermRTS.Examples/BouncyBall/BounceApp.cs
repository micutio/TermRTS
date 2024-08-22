using System.Numerics;
using TermRTS.IO;

namespace TermRTS.Examples.BouncyBall;

internal class BounceWorld : IWorld
{
    public void ApplyChange()
    {
    }
}

internal enum BounceComponentTypes
{
    Ball
}

internal class BounceBall : IComponent
{
    internal BounceBall(float x, float y, float dx, float dy)
    {
        Position = new Vector2(x, y);
        Velocity = new Vector2(dx, dy);
    }

    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }

    public object Clone()
    {
        return new BounceBall(Position.X, Position.Y, Velocity.X, Velocity.Y);
    }
}

// Bouncing ball and other physics:
//  - https://processing.org/examples/bouncingball.html
internal class BouncePhysicsSystem : System<BounceWorld>, IEventSink
{
    private Vector2 _velocity;

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
            }
        }
    }

    public override Dictionary<Type, IComponent>? ProcessComponents(
        ulong timeStepSizeMs,
        EntityBase thisEntityComponents,
        IEnumerable<EntityBase> otherEntityComponents,
        ref BounceWorld world)
    {
        thisEntityComponents
            .Components
            .TryGetValue(typeof(BounceBall), out var changedBallComponent);

        if (changedBallComponent == null)
            return null;

        var maxX = Console.BufferWidth;
        var maxY = Console.BufferHeight;

        var changedBall = (BounceBall)changedBallComponent;
        var ballVel = changedBall.Velocity;
        var ballPos = changedBall.Position;
        ballVel += _velocity;
        _velocity = Vector2.Zero;
        ballPos += ballVel;
        ballVel = Vector2.Multiply(ballVel, 0.90f);

        if (Math.Abs(ballVel.X) < 0.1f)
            ballVel.X = 0.0f;
        if (Math.Abs(ballVel.Y) < 0.1f)
            ballVel.Y = 0.0f;

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

        changedBall.Position = ballPos;
        changedBall.Velocity = ballVel;

        return new Dictionary<Type, IComponent>
            { { typeof(BounceBall), changedBallComponent } };
    }
}

public class BounceApp : IRunnableExample
{
    public void Run()
    {
        var core =
            new Core<BounceWorld>(new BounceWorld(), new BounceRenderer());
        var bouncePhysics = new BouncePhysicsSystem();
        core.AddGameSystem(bouncePhysics);
        var bounceEntity = new EntityBase();
        bounceEntity.AddComponent(new BounceBall(10f, 10f, 0f, 0f));
        core.AddEntity(bounceEntity);

        var scheduler = new Scheduler(16, 16, core);
        scheduler.AddEventSink(core, EventType.Shutdown);
        scheduler.AddEventSink(bouncePhysics, EventType.KeyInput);

        var input = new ConsoleInput();
        scheduler.AddEventSources(input.KeyEventReader);
        input.Run();

        // Run it
        scheduler.SimulationLoop();

        // It should terminate after 12 ticks of 16ms simulated time each.
    }
}