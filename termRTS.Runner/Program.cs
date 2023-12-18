using System.Threading.Channels;
using ConsoleRenderer;

namespace termRTS.Runner
{
    internal static class Program
    {
        private static async Task Main(/* string[] args */)
        {
            // Create Channel for strings
            var channel = Channel.CreateUnbounded<string>();

            var keyInputTask = Task.Run(async () =>
            {
                var writer = channel.Writer;
                var keepRunning = true;
                while (keepRunning)
                {
                    if (!Console.KeyAvailable)
                        continue;

                    var keyInfo = await Task.Run(() => Console.ReadKey(true));
                    if (keyInfo.Key == ConsoleKey.Escape)
                    {
                        keepRunning = false;
                    }
                    await writer.WriteAsync($"key {keyInfo.KeyChar}");
                }
                await writer.WriteAsync("closing channel");
                writer.Complete();
            });

            // main thread reads messages from the channel
            Console.CursorVisible = false;
            var canvas = new ConsoleCanvas()
                .CreateBorder()
                .Render();

            var reader = channel.Reader;
            await foreach (var message in reader.ReadAllAsync())
            {
                canvas.Clear().CreateBorder();
                canvas.Text(2, 2, $"Received: '{message}'");
                canvas.Render();
            }

            await keyInputTask;
        }
    }
}
