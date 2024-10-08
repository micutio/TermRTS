using System.Text;
using TermRTS.Examples.BouncyBall;
using TermRTS.Examples.Circuitry;
using TermRTS.Examples.Testing;

namespace TermRTS.Examples;

internal interface IRunnableExample
{
    void Run();
}

internal static class ExampleRunner
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        switch (args[0])
        {
            case "1":
                Console.WriteLine("Running minimal app...");
                new MinimalApp().Run();
                break;
            case "2":
                Console.WriteLine("Running bounce app...");
                new BounceApp().Run();
                break;
            case "3":
                Console.WriteLine("Running Circuitry App...");
                new App().Run();
                break;
            default:
                Console.WriteLine("Nothing to run...");
                return 1;
        }
        
        Console.WriteLine("done.");
        return 0;
    }
}