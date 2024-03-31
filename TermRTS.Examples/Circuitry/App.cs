namespace TermRTS.Examples.Circuitry
{
    internal class App : IRunnableExample
    {
        public void Run()
        {
            var core = new Core<World, Enum>(new World(), new Renderer());

            var scheduler = new Scheduler(16, 16, core);
            scheduler.AddEventSink(core, EventType.Shutdown);

            var input = new TermRTS.IO.ConsoleInput();
            scheduler.AddEventSources(input.KeyEventReader);
            input.Run();

            // Run it
            scheduler.SimulationLoop();
        }
    }
}
