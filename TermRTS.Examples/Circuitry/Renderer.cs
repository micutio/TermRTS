using ConsoleRenderer;

namespace TermRTS.Examples.Circuitry
{
    internal class Renderer: TermRTS.IRenderer<World, Enum>
    {
        private ConsoleCanvas _canvas;

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

        public void RenderEntity(Dictionary<Enum, IComponent> entity, double howFarIntoNextFrameMs)
        {
        }

        public void FinalizeRender()
        {
            _canvas.Render();
        }
    }
}
