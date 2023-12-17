using ConsoleRenderer;

using System.Threading.Channels;

internal class Program
{
    static async Task Main(string[] args)
    {
        // Create Channel for strings
        var channel = Channel.CreateUnbounded<string>();

        // Start background thread for sending messages
        var senderTask = Task.Run(async () =>
        {
            var writer = channel.Writer;
            for (int i = 0; i < 5; i += 1)
            {
                await writer.WriteAsync($"Message {i}");
                await Task.Delay(1000); // delay to illustrate
            }
            writer.Complete();
        });

        // main thread reads messages from the channel
        var reader = channel.Reader;

        var canvas = new ConsoleCanvas()
            .CreateBorder()
            .Render();

        await foreach (var message in reader.ReadAllAsync())
        {
            canvas.Clear().CreateBorder();
            canvas.Text(1, 1, $"Received {message}");
            canvas.Render();
        }

        await senderTask;

        Console.ReadKey();
    }
}
