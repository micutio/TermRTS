using ConsoleRenderer;
using termRTS.Engine;

namespace termRTS.Runner;

internal static class Program
{
    private static async Task Main(/* string[] args */)
    {
        // main thread reads messages from the channel
        Console.CursorVisible = false;
        var canvas = new ConsoleCanvas()
            .CreateBorder()
            .Render();

        var consoleInput = new Input();
        consoleInput.Run();

        await foreach (var message in consoleInput.KeyEventReader.ReadAllAsync())
        {
            canvas.Clear().CreateBorder();
            canvas.Text(2, 2, $"Received: {message}");
            canvas.Render();
        }
    }
}
