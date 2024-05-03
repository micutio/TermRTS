using System.Numerics;
using ConsoleRenderer;

namespace TermRTS.Examples.Circuitry
{
    internal class Renderer : TermRTS.IRenderer<World, App.CircuitComponentTypes>
    {
        private readonly ConsoleCanvas _canvas;
        public Vector2 Size = new(Console.WindowWidth, Console.WindowHeight);
        public Vector2 CameraPos = new Vector2(0, 0);
        public Vector2 CameraSize = new Vector2(Console.WindowWidth, Console.WindowHeight);

        public Renderer()
        {
            Console.CursorVisible = false;
            _canvas = new ConsoleCanvas().Render();
        }

        public void RenderWorld(World world, double howFarIntoNextFrameMs)
        {
            _canvas.Clear();
            _canvas.Text(1, 1, "Circuitry World");
        }

        public void RenderEntity(Dictionary<App.CircuitComponentTypes, IComponent> entity, double howFarIntoNextFrameMs)
        {
            if (!entity.TryGetValue(App.CircuitComponentTypes.Chip, out var chipComponent))
                return;

            var chip = (App.Chip)chipComponent;
            var x1 = chip.Position1.X;
            var x2 = chip.Position2.X;
            var y1 = chip.Position1.Y;
            var y2 = chip.Position2.Y;

            // left upper corner
            if (IsInCamera(x1, y1))
            {
                _canvas.Set((int)(x1 - CameraPos.X), (int)(y1 - CameraPos.Y), Cp437.BoxDoubleDownDoubleRight);
            }

            // left lower corner
            if (IsInCamera(x1, y2))
            {
                _canvas.Set((int)(x1 - CameraPos.X), (int)(y2 - CameraPos.Y), Cp437.BoxDoubleUpDoubleRight);
            }

            // right upper corner
            if (IsInCamera(x2, y1))
            {
                _canvas.Set((int)(x2 - CameraPos.X), (int)(y1 - CameraPos.Y), Cp437.BoxDoubleDownDoubleLeft);
            }

            // right lower corner
            if (IsInCamera(x2, y2))
            {
                _canvas.Set((int)(x2 - CameraPos.X), (int)(y2 - CameraPos.Y), Cp437.BoxDoubleUpDoubleLeft);
            }

            var minX = Math.Max(x1, CameraPos.X) + 1;
            var minY = Math.Max(y1, CameraPos.Y) + 1;
            var maxX = Math.Min(x2, CameraSize.X + CameraPos.X) - 1;
            var maxY = Math.Min(y2, CameraSize.Y + CameraPos.Y) - 1;

            // upper and lower wall
            for (var i = minX; i <= maxX; i += 1)
            {
                _canvas.Set((int)(i - CameraPos.X), (int)(y1 - CameraPos.Y), Cp437.BoxDoubleHorizontal);
                _canvas.Set((int)(i - CameraPos.X), (int)(y2 - CameraPos.Y), Cp437.BoxDoubleHorizontal);
            }

            // left and right wall
            for (var i = minY; i <= maxY; i += 1)
            {
                _canvas.Set((int)(x1 - CameraPos.X), (int)(i - CameraPos.Y), Cp437.BoxDoubleVertical);
                _canvas.Set((int)(x2 - CameraPos.X), (int)(i - CameraPos.Y), Cp437.BoxDoubleVertical);
            }
        }

        public void FinalizeRender()
        {
            _canvas.Render();
        }

        private bool IsInCamera(float x, float y)
        {
            return (x >= CameraPos.X && y <= CameraSize.X - CameraPos.X)
                   && (y >= CameraPos.Y && y <= CameraSize.Y - CameraPos.Y);
        }
    }
}
