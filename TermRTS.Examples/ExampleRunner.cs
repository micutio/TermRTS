using System.Text;
using log4net.Config;
using TermRTS.Examples.BouncyBall;
using TermRTS.Examples.Minimal;

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
        XmlConfigurator.Configure();
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
                new Circuitry.Circuitry().Run();
                break;
            case "4":
                Console.WriteLine("Running Greenery App...");
                new Greenery.Greenery().Run();
                break;
            default:
                Console.WriteLine("Nothing to run...");
                return 1;
        }

        Console.WriteLine("done.");
        return 0;
    }
}