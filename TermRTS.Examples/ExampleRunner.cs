using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TermRTS.Examples.BouncyBall;
using TermRTS.Examples.Minimal;
using TermRTS.Log;

namespace TermRTS.Examples;

internal interface IRunnableExample
{
    void Run();
}

internal static class ExampleRunner
{
    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: dotnet run --project TermRTS.Examples -- <example number>");
            Console.WriteLine("  1 = Minimal App, 2 = Bouncy Ball, 3 = Circuitry, 4 = Greenery");
            return 1;
        }

        Console.OutputEncoding = Encoding.UTF8;

        // TODO: Maybe move that into TermRTS.Log .
        using var factory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(new RollingFileLoggerProvider(
                $"TermRTS.{Environment.ProcessId}.log",
                25L * 1024 * 1024,
                5));
        });
        TermRtsLog.Factory = factory;

        try
        {
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

            return 0;
        }
        finally
        {
            TermRtsLog.Factory = NullLoggerFactory.Instance;
        }
    }
}