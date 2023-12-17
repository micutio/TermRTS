using ConsoleRenderer;

using System.Threading.Channels;

internal class Program
{
    static async Task Main(string[] args)
    {
        // Create Channel for strings
        var channel = Channel.CreateUnbounded<string>();

        var keyInputTask = Task.Run(async () =>
        {
            var writer = channel.Writer;
            var keepRunning = true;
            while (keepRunning)
            {
                if (!Console.KeyAvailable) continue;

                ConsoleKeyInfo keyInfo = await Task.Run(() => Console.ReadKey(true));
                if (keyInfo.Key == ConsoleKey.Escape)
                {
                    keepRunning = false;
                }
                await writer.WriteAsync($"key press detected {keyInfo.KeyChar}");
            }
            await writer.WriteAsync("Closing channel.");
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
            canvas.Text(1, 1, $"Received {message}");
            canvas.Render();
        }

        //await
        Task.WhenAll(keyInputTask).Wait();
    }
}
