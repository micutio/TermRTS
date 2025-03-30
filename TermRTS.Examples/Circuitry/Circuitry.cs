using System.Numerics;
using log4net;
using TermRTS.Event;
using TermRTS.Io;

namespace TermRTS.Examples.Circuitry;

internal class Circuitry : IRunnableExample
{
    private readonly ILog _log;

    public Circuitry()
    {
        _log = LogManager.GetLogger(GetType());
    }

    /// <summary>
    ///     Main method of the app.
    /// </summary>
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

        _log.Info("Launching Circuitry example!");

        var renderer = new Renderer();
        var core = new Core(renderer);
        EntityGenerator.RandomCircuitBoard()
            .WithRandomSeed(0)
            .WithChipCount(20)
            .WithChipDimensions(5, 15)
            .WithBusDimensions(1, 8)
            .WithWorldDimensions(Console.WindowWidth, Console.WindowHeight)
            .Build(out var entities, out var components);
        core.AddAllEntities(entities);
        core.AddAllComponents(components);
        core.AddSimSystem(new BusSystem());

        var scheduler = new Scheduler(core);
        scheduler.AddEventSources(scheduler.ProfileEventReader);
        scheduler.AddEventSink(renderer, EventType.Profile);

        var input = new ConsoleInput();
        scheduler.AddEventSources(input.KeyEventReader);
        scheduler.AddEventSink(input, EventType.Shutdown);
        input.Run();

        // Graceful shutdown on canceling via CTRL+C
        Console.CancelKeyPress += delegate(object? _, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            scheduler.EnqueueEvent((new PlainEvent(EventType.Shutdown), 0L));
        };

        // Shutdown after one hour
        scheduler.EnqueueEvent((new PlainEvent(EventType.Shutdown), 1000 * 60 * 60));

        // Run it
        var simulation = new Simulation(scheduler);
        simulation.Run();

        // After the app is terminated, clear the console.
        Console.Clear();

        _log.Info("Shutting down Circuitry example!");
    }

    #region Internal Types

    private enum Direction
    {
        North,
        East,
        South,
        West
    }

    internal readonly record struct Cell(int X, int Y, char C)
    {
    }

    internal class Chip : ComponentBase
    {
        public Chip(int entityId, Vector2 position1, Vector2 position2) : base(entityId)
        {
            Position1 = position1;
            Position2 = position2;
            Outline = GenerateOutline();
        }

        internal Vector2 Position1 { get; }
        internal Vector2 Position2 { get; }

        public Cell[] Outline { get; }

        public bool IsIntersecting(Chip other)
        {
            return Position1.X <= other.Position2.X
                   && Position2.X >= other.Position1.X
                   && Position1.Y <= other.Position2.Y
                   && Position2.Y >= other.Position1.Y;
        }

        public Vector2 Center()
        {
            var newX = (Position1.X + Position2.X) / 2.0f;
            var newY = (Position1.Y + Position2.Y) / 2.0f;
            return new Vector2(newX, newY);
            //return Vector2.Lerp(Position1, Position2, 0.5f);
        }

        // TODO: Does it make sense to implement the wall getters as extension methods because they're only used in one place?

        public ArraySegment<Cell> UpperWall()
        {
            return new ArraySegment<Cell>(Outline, 4, Convert.ToInt32(Position2.X - Position1.X) - 1);
        }

        public ArraySegment<Cell> LowerWall()
        {
            var width = Convert.ToInt32(Position2.X - Position1.X) - 1;
            return new ArraySegment<Cell>(Outline, 4 + width, width);
        }

        public ArraySegment<Cell> LeftWall()
        {
            var width = Convert.ToInt32(Position2.X - Position1.X) - 1;
            var height = Convert.ToInt32(Position2.Y - Position1.Y) - 1;
            // return Outline.Skip(4 + width + width).Take(height).ToList();
            return new ArraySegment<Cell>(Outline, 4 + width + width, height);
        }

        public ArraySegment<Cell> RightWall()
        {
            var width = Convert.ToInt32(Position2.X - Position1.X) - 1;
            var height = Convert.ToInt32(Position2.Y - Position1.Y) - 1;
            // return Outline.Skip(4 + width + width + height).Take(height).ToList();
            return new ArraySegment<Cell>(Outline, 4 + width + width + height, height);
        }

        private Cell[] GenerateOutline()
        {
            var outline = new List<Cell>();
            var x1 = Convert.ToInt32(Position1.X);
            var x2 = Convert.ToInt32(Position2.X);
            var y1 = Convert.ToInt32(Position1.Y);
            var y2 = Convert.ToInt32(Position2.Y);

            // left upper corner
            outline.Add(new Cell(x1, y1, Cp437.BoxDoubleDownDoubleRight));

            // left lower corner
            outline.Add(new Cell(x1, y2, Cp437.BoxDoubleUpDoubleRight));

            // right upper corner
            outline.Add(new Cell(x2, y1, Cp437.BoxDoubleDownDoubleLeft));

            // right lower corner
            outline.Add(new Cell(x2, y2, Cp437.BoxDoubleUpDoubleLeft));

            // lower wall
            for (var i = x1 + 1; i < x2; i += 1)
                outline.Add(new Cell(i, y1, Cp437.BoxDoubleHorizontal));

            // upper wall
            for (var i = x1 + 1; i < x2; i += 1)
                outline.Add(new Cell(i, y2, Cp437.BoxDoubleHorizontal));

            // left wall
            for (var i = y1 + 1; i < y2; i += 1)
                outline.Add(new Cell(x1, i, Cp437.BoxDoubleVertical));

            // right wall
            for (var i = y1 + 1; i < y2; i += 1)
                outline.Add(new Cell(x2, i, Cp437.BoxDoubleVertical));

            return outline.ToArray();
        }
    }

    internal class Bus : ComponentBase
    {
        public const float Velocity = 25.5f; // in [m/s]
        private readonly DoubleBuffered<bool> _isActive;
        private readonly DoubleBuffered<bool> _isForward;

        private readonly DoubleBuffered<float> _progress;
        public readonly List<Wire> Connections;

        public Bus(int eid, List<Wire> connections) : base(eid)
        {
            Connections = connections;
            _progress = new DoubleBuffered<float>(0); // in [%]
            _isActive = new DoubleBuffered<bool>(false);
            _isForward = new DoubleBuffered<bool>(true);
            RegisterDoubleBufferedProperty(_progress);
            RegisterDoubleBufferedProperty(_isActive);
            RegisterDoubleBufferedProperty(_isForward);
        }

        public float Progress
        {
            get => _progress.Get();
            set
            {
                if (_progress.Get() > 1.0f)
                {
                    IsActive = false;
                    _progress.Set(0.0f);
                }
                else
                {
                    _progress.Set(value);
                }
            }
        }

        public bool IsActive
        {
            get => _isActive.Get();
            set => _isActive.Set(value);
        }

        public bool IsForward
        {
            get => _isForward.Get();
            set => _isForward.Set(value);
        }

        public int AvgWireLength
        {
            get
            {
                var sumOfLengths = Connections
                    .ConvertAll(c => c.Outline.Length)
                    .Sum();
                return sumOfLengths / Connections.Count;
            }
        }
    }

    internal class Wire
    {
        public Wire(IList<(int x, int y)> positions)
        {
            Outline = new Cell[positions.Count];
            var positionCount = positions.Count;

            // Generate starting terminator
            var startChar = GenerateTerminatorChar(
                positions[0].x,
                positions[0].y,
                positions[1].x,
                positions[1].y
            );
            Outline[0] = new Cell(positions[0].x, positions[0].y, startChar);

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
                Outline[i] = new Cell(positions[i].x, positions[i].y, c);
            }

            // Generate ending terminator
            var endChar = GenerateTerminatorChar(
                positions[positionCount - 1].x,
                positions[positionCount - 1].y,
                positions[positionCount - 2].x,
                positions[positionCount - 2].y
            );
            Outline[positionCount - 1] = new Cell(positions[positionCount - 1].x,
                positions[positionCount - 1].y, endChar);
        }

        // x,y coordinates and visual representation
        public Cell[] Outline { get; }

        private static char GenerateTerminatorChar(int thisX, int thisY, int nextX, int nextY)
        {
            if (thisX != nextX)
                return thisX > nextX
                    ? Cp437.BoxDoubleVerticalLeft
                    : Cp437.BoxDoubleVerticalRight;
            return thisY > nextY
                ? Cp437.BoxUpDoubleHorizontal
                : Cp437.BoxDownDoubleHorizontal;
        }

        private static char GenerateWireChar(int thisX, int thisY, int prevX, int prevY, int nextX,
            int nextY)
        {
            Direction incoming;
            if (thisX != prevX)
                incoming = thisX > prevX
                    ? Direction.West
                    : Direction.East;
            else
                incoming = thisY > prevY
                    ? Direction.North
                    : Direction.South;

            Direction outgoing;
            if (thisX != nextX)
                outgoing = thisX > nextX
                    ? Direction.West
                    : Direction.East;
            else
                outgoing = thisY > nextY
                    ? Direction.North
                    : Direction.South;

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
    }

    private class BusSystem : ISimSystem
    {
        private readonly Random _rng = new();
        private ulong _timeSinceLastAttempt;

        public void ProcessComponents(ulong timeStepSizeMs, in IStorage storage)
        {
            foreach (var bus in storage.GetAllForType<Bus>())
            {
                // If not active, then randomly determine whether to activate.
                if (!bus.IsActive)
                {
                    if (_timeSinceLastAttempt >= 1000L)
                    {
                        _timeSinceLastAttempt = 0L;
                        if (_rng.NextSingle() > 0.02) continue;

                        bus.IsActive = true;
                        bus.IsForward = _rng.Next() % 2 == 0;
                        bus.Progress = 0.0f;
                    }
                    else
                    {
                        _timeSinceLastAttempt += timeStepSizeMs;
                    }

                    continue;
                }

                //  If already active, then take speed, divide by time step size and advance progress
                var progressInM = bus.AvgWireLength * bus.Progress;
                var deltaDistInM = Bus.Velocity / 1000.0f * timeStepSizeMs;
                bus.Progress = (progressInM + deltaDistInM) / bus.AvgWireLength;
            }
        }
    }

    #endregion
}