using System.Numerics;

namespace TermRTS.Examples.Circuitry;

internal class App : IRunnableExample
{
    internal enum CircuitComponentTypes { Chip, Bus }

    internal enum Direction { North, East, South, West }

    internal class Chip : IComponent
    {
        public Vector2 Position1;
        public Vector2 Position2;
        public List<(int, int, char)> Outline;

        public Chip(Vector2 position1, Vector2 position2)
        {
            Position1 = position1;
            Position2 = position2;
            Outline = GenerateOutline();
        }

        public bool IsIntersecting(Chip other)
        {
            return Position1.X <= other.Position2.X
                   && Position2.X >= other.Position1.X
                   && Position1.Y <= other.Position2.Y
                   && Position2.Y >= other.Position1.Y;
        }

        public Vector2 Center()
        {
            var newX = (int)((Position1.X + Position2.X) / 2);
            var newY = (int)((Position1.X + Position2.X) / 2);
            return new Vector2(newX, newY);
        }

        public object Clone()
        {
            return new Chip(
                new Vector2(Position1.X, Position1.Y),
                new Vector2(Position2.X, Position2.Y));
        }

        private List<(int, int, char)> GenerateOutline()
        {
            var outline = new List<(int, int, char)>();
            var x1 = (int)Position1.X;
            var x2 = (int)Position2.X;
            var y1 = (int)Position1.Y;
            var y2 = (int)Position2.Y;

            // left upper corner
            outline.Add((x1, y1, Cp437.BoxDoubleDownDoubleRight));

            // left lower corner
            outline.Add((x1, y2, Cp437.BoxDoubleUpDoubleRight));

            // right upper corner
            outline.Add((x2, y1, Cp437.BoxDoubleDownDoubleLeft));

            // right lower corner
            outline.Add((x2, y2, Cp437.BoxDoubleUpDoubleLeft));

            // upper and lower wall
            for (var i = x1 + 1; i < x2; i += 1)
            {
                outline.Add((i, y1, Cp437.BoxDoubleHorizontal));
                outline.Add((i, y2, Cp437.BoxDoubleHorizontal));
            }

            // left and right wall
            for (var i = y1 + 1; i < y2; i += 1)
            {
                outline.Add((x1, i, Cp437.BoxDoubleVertical));
                outline.Add((x2, i, Cp437.BoxDoubleVertical));
            }
            return outline;
        }
    }

    internal class Bus : IComponent
    {
        public List<Wire> Connections;

        public const float Velocity = 25.5f; // in [m/s]
        private float _progress; // in [%]

        public Bus(List<Wire> connections)
        {
            Connections = connections;
            IsActive = false;
            IsForward = true;
        }

        public bool IsActive
        {
            get;
            set;
        }

        public bool IsForward
        {
            get;
            set;
        }

        public int AvgWireLength
        {
            get
            {
                var sumOfLengths = Connections
                    .ConvertAll(c => c.Outline.Count)
                    .Sum();
                return sumOfLengths / Connections.Count;
            }
        }

        public float Progress
        {
            get => _progress;
            set
            {
                if (_progress > 1.0f)
                {
                    IsActive = false;
                    _progress = 0.0f;
                }
                else
                {
                    _progress = value;
                }
            }
        }

        public object Clone()
        {
            return new Bus(new List<Wire>(Connections));
        }
    }

    internal class Wire : IComponent
    {
        // x,y coordinates and visual representation
        public List<(int, int, char)> Outline;

        public Wire(IReadOnlyList<(int x, int y)> positions)
        {
            Outline = new List<(int, int, char)>();
            var positionCount = positions.Count;

            // Generate starting terminator
            var startChar = GenerateTerminatorChar(
                positions[0].x,
                positions[0].y,
                positions[1].x,
                positions[1].y
            );
            Outline.Add((positions[0].x, positions[0].y, startChar));

            // Generate all parts in-between
            for (var i = 1; i < positionCount - 1; ++i)
            {
                var c = GenerateWireChar(
                    positions[i].x,
                    positions[i].y,
                    positions[i - 1].x,
                    positions[i - 1].y,
                    positions[i + 1].x,
                    positions[i + 1].y
                );
                Outline.Add((positions[i].x, positions[i].y, c));
            }

            // Generate ending terminator
            var endChar = GenerateTerminatorChar(
                positions[positionCount - 1].x,
                positions[positionCount - 1].y,
                positions[positionCount - 2].x,
                positions[positionCount - 2].y
            );
            Outline.Add((positions[positionCount - 1].x, positions[positionCount - 1].y, endChar));
        }

        private static char GenerateTerminatorChar(int thisX, int thisY, int nextX, int nextY)
        {
            if (thisX != nextX)
            {
                return thisX > nextX
                    ? Cp437.BoxDoubleVerticalLeft
                    : Cp437.BoxDoubleVerticalRight;
            }
            return thisY > nextY
                ? Cp437.BoxUpDoubleHorizontal
                : Cp437.BoxDownDoubleHorizontal;
        }

        private static char GenerateWireChar(int thisX, int thisY, int prevX, int prevY, int nextX, int nextY)
        {
            Direction incoming;
            if (thisX != prevX)
            {
                incoming = thisX > prevX
                    ? Direction.West
                    : Direction.East;
            }
            else
            {
                incoming = thisY > prevY
                    ? Direction.North
                    : Direction.South;
            }

            Direction outgoing;
            if (thisX != nextX)
            {
                outgoing = thisX > nextX
                    ? Direction.West
                    : Direction.East;
            }
            else
            {
                outgoing = thisY > nextY
                    ? Direction.North
                    : Direction.South;
            }

            return (incoming, outgoing) switch
            {
                (Direction.North, Direction.East) or
                    (Direction.East, Direction.North) => Cp437.BoxUpRight,
                (Direction.North, Direction.South) or
                    (Direction.South, Direction.North) => Cp437.BoxVertical,
                (Direction.North, Direction.West) or
                    (Direction.West, Direction.North) => Cp437.BoxUpLeft,
                (Direction.West, Direction.East) or
                    (Direction.East, Direction.West) => Cp437.BoxHorizontal,
                (Direction.West, Direction.South) or
                    (Direction.South, Direction.West) => Cp437.BoxDownLeft,
                (Direction.South, Direction.East) or
                    (Direction.East, Direction.South) => Cp437.BoxDownRight,
                _ => '?'
            };
        }

        public object Clone()
        {
            var outline = Outline.Select(item => (item.Item1, item.Item2)).ToList();
            return new Wire(outline);
        }
    }

    public class BusSystem : System<World, CircuitComponentTypes>
    {
        private readonly Random _rng = new();
        private ulong _timeSinceLastAttempt;

        public override Dictionary<CircuitComponentTypes, IComponent>? ProcessComponents(
            ulong timeStepSizeMs,
            EntityBase<CircuitComponentTypes> thisEntityComponents,
            List<EntityBase<CircuitComponentTypes>> otherEntityComponents,
            ref World world)
        {
            thisEntityComponents
                .Components
                .TryGetValue(CircuitComponentTypes.Bus, out var busComponent);

            if (busComponent == null)
                return new Dictionary<CircuitComponentTypes, IComponent>();

            var bus = (Bus)busComponent;

            // If not active, then randomly determine whether to activate.
            if (!bus.IsActive)
            {
                if (_timeSinceLastAttempt >= 1000L)
                {
                    _timeSinceLastAttempt = 0L;
                    if (_rng.NextSingle() < 0.5)
                    {
                        bus.IsActive = true;
                        bus.IsForward = _rng.Next() % 2 == 0;
                        bus.Progress = 0.0f;
                    }
                }
                else
                {
                    _timeSinceLastAttempt += timeStepSizeMs;
                }
                return new Dictionary<CircuitComponentTypes, IComponent> { { CircuitComponentTypes.Bus, bus } };
            }

            //  If already active, then take speed, divide by time step size and advance progress
            var progressInM = bus.AvgWireLength * bus.Progress;
            var deltaDistInM = (Bus.Velocity / 1000.0f) * timeStepSizeMs;
            bus.Progress = (progressInM + deltaDistInM) / bus.AvgWireLength;

            return new Dictionary<CircuitComponentTypes, IComponent> { { CircuitComponentTypes.Bus, bus } };
        }
    }

    public void Run()
    {
        // Add two chips and a wire to test

        // TODO: Create a system for generating chips and buses:
        //       [x] A class which generates everything first and hands it over to the core
        //       [ ] Later turn that class into a system and hand over a subset of items every x ticks
        //       [ ] Finally hand over items immediately after creation and generate them slowly at runtime
        // TODO: Render world during generation
        // TODO: Generate chips atomically and wires bit by bit
        // TODO: How to deal with unfinished wires? Currently generated in full

        var busSystem = new BusSystem();
        var renderer = new Renderer();

        var core = new Core<World, CircuitComponentTypes>(new World(), renderer);
        foreach (var e in EntityGenerator.RandomCircuitBoard().Build())
        //foreach (var e in EntityGenerator.BuildSmallCircuitBoard())
        {
            core.AddEntity(e);
        }
        core.AddGameSystem(busSystem);

        var scheduler = new Scheduler(16, 16, core);
        scheduler.AddEventSources(scheduler.ProfileEventReader);
        scheduler.AddEventSink(core, EventType.Shutdown);
        scheduler.AddEventSink(renderer, EventType.Profile);

        var input = new TermRTS.IO.ConsoleInput();
        scheduler.AddEventSources(input.KeyEventReader);
        scheduler.AddEventSink(input, EventType.Shutdown);
        input.Run();

        Console.CancelKeyPress += delegate (object? _, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            scheduler.EnqueueEvent((new PlainEvent(EventType.Shutdown), 0L));
        };

        // Run it
        scheduler.SimulationLoop();

        Console.Clear();
        Console.WriteLine("Application Terminated!");
    }
}