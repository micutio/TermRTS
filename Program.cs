using ConsoleRenderer;

internal class Program
{
    private static void Main(string[] args)
    {
        var canvas = new ConsoleCanvas()
            .CreateBorder()
            .Render();

        Console.ReadKey();
    }
}
