using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TermRTS.Examples.BouncyBall;
using TermRTS.Examples.Testing;

namespace TermRTS.Examples
{
    internal interface IRunnableExample
    {
        void Run();
    }

    internal static class ExampleRunner
    {

        static int Main(string[] args)
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
                default:
                    Console.WriteLine("Nothing to run...");
                    return 1;
            }
            Console.WriteLine("done.");
            return 0;
        }
    }
}
