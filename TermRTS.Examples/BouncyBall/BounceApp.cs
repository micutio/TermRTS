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

    public float X { get; set; }
    public float Y { get; set; }

    public float DeltaX { get; set; }
    public float DeltaY { get; set; }

    internal BounceBall(float x, float y, float dx, float dy)
    {
        X = x;
        Y = y;
        DeltaX = dx;
        DeltaY = dy;
    }

    public object Clone()
    {
        return new BounceBall(X, Y, DeltaX, DeltaY);
    }
}

internal class BounceEntity : EntityBase<BounceComponents> { }

internal class BouncePhysicsSystem : System<BounceWorld, BounceComponents>, IEventSink
{
    private float _forceX;
    private float _forceY;

    public override Dictionary<BounceComponents, IComponent>? ProcessComponents(
            UInt64 timeStepSizeMs,
            EntityBase<BounceComponents> thisEntityComponents,
            List<EntityBase<BounceComponents>> otherEntityComponents,
            ref BounceWorld world)
    {
        if (!thisEntityComponents.Components.ContainsKey(BounceComponents.Ball))
            return new Dictionary<BounceComponents, IComponent>();

        var changedBallComponent = (BounceBall)thisEntityComponents.Components[BounceComponents.Ball];
        changedBallComponent.DeltaX += _forceX;
        changedBallComponent.DeltaY += _forceY;
        _forceX = 0.0f;
        _forceY = 0.0f;
        changedBallComponent.X += changedBallComponent.DeltaX;
        changedBallComponent.Y += changedBallComponent.DeltaY;
        changedBallComponent.DeltaX -= (changedBallComponent.DeltaX * 0.45f * (timeStepSizeMs / 1000.0f));
        if (changedBallComponent.DeltaX < 0.01f)
            changedBallComponent.DeltaX = 0.0f;
        changedBallComponent.DeltaY -= (changedBallComponent.DeltaY * 0.45f * (timeStepSizeMs / 1000.0f));
        if (changedBallComponent.DeltaY < 0.01f)
            changedBallComponent.DeltaY = 0.0f;

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
                    _forceX -= 1;
                    break;
                case ConsoleKey.D:
                    _forceX += 1;
                    break;
                case ConsoleKey.W:
                    _forceY -= 1;
                    break;
                case ConsoleKey.S:
                    _forceY += 1;
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
