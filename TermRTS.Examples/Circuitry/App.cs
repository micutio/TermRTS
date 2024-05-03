using System.Numerics;

namespace TermRTS.Examples.Circuitry
{
    internal class App : IRunnableExample
    {

        internal enum CircuitComponentTypes
        {
            Chip
        }

        internal class Chip : IComponent
        {
            public Vector2 Position1;
            public Vector2 Position2;

            public Chip(Vector2 position1, Vector2 position2)
            {
                Position1 = position1;
                Position2 = position2;
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
        }

        internal class ChipEntity : EntityBase<CircuitComponentTypes>
        { }

        public void Run()
        {
            var core = new Core<World, CircuitComponentTypes>(new World(), new Renderer());

            var scheduler = new Scheduler(16, 16, core);
            scheduler.AddEventSink(core, EventType.Shutdown);

            // Add a chip to test
            var chipEntity = new ChipEntity();
            chipEntity.AddComponent(
                CircuitComponentTypes.Chip,
                new Chip(new Vector2(20, 20), new Vector2(25, 25)));
            core.AddEntity(chipEntity);

            var input = new TermRTS.IO.ConsoleInput();
            scheduler.AddEventSources(input.KeyEventReader);
            input.Run();

            // Run it
            scheduler.SimulationLoop();
        }
    }
}
