using System.Numerics;

namespace TermRTS.Examples.Circuitry;

internal class App : IRunnableExample
{

    internal enum CircuitComponentTypes { Chip, Wire }

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

    internal class Wire : IComponent
    {
        // x,y coordinates and visual representation
        public List<(int, int, char)> Outline;

        public Wire(IReadOnlyList<(int x, int y)> positions)
        {
            Outline = new List<(int, int, char)>();
            var positionCount = positions.Count;

            var startChar = GenerateTerminatorChar(
                positions[0].x,
                positions[0].y,
                positions[1].x,
                positions[1].y
            );
            Outline.Add((positions[0].x, positions[0].y, startChar));

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

    public void Run()
    {
        var core = new Core<World, CircuitComponentTypes>(new World(), new Renderer());

        var scheduler = new Scheduler(16, 16, core);
        scheduler.AddEventSink(core, EventType.Shutdown);

        // Add two chips and a wire to test
        var chipEntity1 = new EntityBase<CircuitComponentTypes>();
        chipEntity1.AddComponent(
            CircuitComponentTypes.Chip,
            new Chip(new Vector2(10, 10), new Vector2(15, 15)));
        core.AddEntity(chipEntity1);

        var chipEntity2 = new EntityBase<CircuitComponentTypes>();
        chipEntity2.AddComponent(
            CircuitComponentTypes.Chip,
            new Chip(new Vector2(20, 20), new Vector2(30, 30)));
        core.AddEntity(chipEntity2);

        var wireEntity1 = new EntityBase<CircuitComponentTypes>();
        wireEntity1.AddComponent(
            CircuitComponentTypes.Wire,
            new Wire(
                new List<(int, int)>
                { 
                    // from the lower chip, first go left
                    (20, 25), (19, 25), (18, 25), (17, 25), (16, 25), (15, 25), (14, 25), (13, 25),
                    // then go up
                    (13, 24), (13, 23), (13, 22), (13, 21), (13, 20), (13, 19), (13, 18), (13, 17),
                    // now go right
                    (14, 17), (15, 17), (16, 17), (17, 17), (18, 17),
                    // go up again
                    (18, 16), (18, 15), (18, 14), 
                    // finally, go left and connect to the smaller chip
                    (17, 14), (16, 14), (15, 14)
                })
            );
        core.AddEntity(wireEntity1);

        var wireEntity2 = new EntityBase<CircuitComponentTypes>();
        wireEntity2.AddComponent(
            CircuitComponentTypes.Wire,
            new Wire(
                new List<(int, int)>
                { 
                    // from the lower chip, first go left
                    (20, 24), (19, 24), (18, 24), (17, 24), (16, 24), (15, 24), (14, 24),
                    // then go up
                    (14, 23), (14, 22), (14, 21), (14, 20), (14, 19), (14, 18),
                    // now go right
                    (15, 18), (16, 18), (17, 18), (18, 18), (19, 18),
                    // go up again
                    (19, 17), (19, 16), (19, 15), (19, 14), (19, 13),
                    // finally, go left and connect to the smaller chip
                    (18, 13), (17, 13), (16, 13), (15, 13)
                })
            );
        core.AddEntity(wireEntity2);

        var wireEntity3 = new EntityBase<CircuitComponentTypes>();
        wireEntity3.AddComponent(
            CircuitComponentTypes.Wire,
            new Wire(
                new List<(int, int)>
                { 
                    // from the lower chip, first go left
                    (20, 23), (19, 23), (18, 23), (17, 23), (16, 23), (15, 23),
                    // then go up
                    (15, 22), (15, 21), (15, 20), (15, 19),
                    // now go right
                    (16, 19), (17, 19), (18, 19), (19, 19), (20, 19),
                    // go up again
                    (20, 18), (20, 17), (20, 16), (20, 15), (20, 14), (20, 13), (20, 12),
                    // finally, go left and connect to the smaller chip
                    (19, 12), (18, 12), (17, 12), (16, 12), (15, 12)
                })
            );
        core.AddEntity(wireEntity3);

        var wireEntity4 = new EntityBase<CircuitComponentTypes>();
        wireEntity4.AddComponent(
            CircuitComponentTypes.Wire,
            new Wire(new List<(int x, int y)>
            {
                // from the upper chip, first go up
                (14, 10), (14, 9),
                // then turn right
                (15, 9), (16, 9), (17, 9), (18, 9), (19, 9), (20, 9), (21, 9),
                (22, 9), (23, 9), (24, 9), (25, 9), (26, 9), (27, 9), (28, 9),
                // then turn downwards
                (28, 10), (28, 11), (28, 12), (28, 13), (28, 14), (28, 15),
                (28, 16), (28, 17), (28, 18), (28, 19), (28, 20)
            })
        );
        core.AddEntity(wireEntity4);

        var wireEntity5 = new EntityBase<CircuitComponentTypes>();
        wireEntity5.AddComponent(
            CircuitComponentTypes.Wire,
            new Wire(new List<(int x, int y)> { (7, 7), (7, 8), (8, 8), (8, 7) }));
        core.AddEntity(wireEntity5);

        var input = new TermRTS.IO.ConsoleInput();
        scheduler.AddEventSources(input.KeyEventReader);
        input.Run();

        // Run it
        scheduler.SimulationLoop();
    }
}