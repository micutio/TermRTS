using System.Numerics;
using TermRTS.Events;
using TermRTS.Io;

namespace TermRTS.Examples.BouncyBall;

internal class BounceBall : ComponentBase
{
    private readonly DoubleBuffered<Vector2> _position;
    private readonly DoubleBuffered<Vector2> _velocity;

    internal BounceBall(int id, float x, float y, float dx, float dy) : base(id)
    {
        _position = new DoubleBuffered<Vector2>(new Vector2(x, y));
        _velocity = new DoubleBuffered<Vector2>(new Vector2(dx, dy));

        RegisterDoubleBufferedProperty(_position);
        RegisterDoubleBufferedProperty(_velocity);
    }

    public Vector2 Position
    {
        get => _position.Get();
        set => _position.Set(value);
    }

    public Vector2 Velocity
    {
        get => _velocity.Get();
        set => _velocity.Set(value);
    }
}

// Bouncing ball and other physics:
//  - https://processing.org/examples/bouncingball.html
internal class BouncePhysicsSystem : SimSystem, IEventSink
{
    private Vector2 _velocity;

    public void ProcessEvent(IEvent evt)
    {
        if (evt.Type() != EventType.KeyInput) return;

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

    public override void ProcessComponents(ulong timeStepSizeMs, in IStorage storage)
    {
        var ballComponents = storage.GetForType(typeof(BounceBall));

        foreach (var ballComponent in ballComponents)
        {
            var ball = (BounceBall)ballComponent;

            var maxX = Console.BufferWidth;
            var maxY = Console.BufferHeight;

            var ballVel = ball.Velocity;
            var ballPos = ball.Position;
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

            ball.Position = ballPos;
            ball.Velocity = ballVel;
        }
    }
}

public class BounceApp : IRunnableExample
{
    public void Run()
    {
        var core = new Core(new BounceRenderer());
        var bouncePhysics = new BouncePhysicsSystem();
        core.AddSimSystem(bouncePhysics);
        var bounceEntity = new EntityBase();
        var bounceBall = new BounceBall(bounceEntity.Id, 10f, 10f, 0f, 0f);
        core.AddEntity(bounceEntity);
        core.AddComponent(bounceBall);

        var scheduler = new Scheduler(16, 16, core);
        scheduler.AddEventSink(core, EventType.Shutdown);
        scheduler.AddEventSink(bouncePhysics, EventType.KeyInput);

        var input = new ConsoleInput();
        scheduler.AddEventSources(input.KeyEventReader);
        input.Run();

        scheduler.SimulationLoop();
    }
}