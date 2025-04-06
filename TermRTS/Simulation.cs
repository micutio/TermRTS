using log4net;
using TermRTS.Event;

namespace TermRTS;

/// <summary>
///     Top-level class representing a simulation or game.
///     Main purpose is to encapsulate the Scheduler and Core to allow for convenient de/serialisation.
///     TODO: Error handling and visible feedback problem solution for user.
///     See
///     https://madhawapolkotuwa.medium.com/mastering-json-serialization-in-c-with-system-text-json-01f4cec0440d
/// </summary>
public class Simulation(Scheduler scheduler) : IEventSink
{
    #region IEventSink Members

    public void ProcessEvent(IEvent evt)
    {
        if (evt.Type() != EventType.Persistence ||
            evt is not PersistenceEvent persistenceEvent) return;

        switch (persistenceEvent.option)
        {
            case PersistenceOption.Load:
                // TODO: Handle exceptions in loading from file
                var loadedJsonStr = Persistence.LoadJsonFromFile(persistenceEvent.jsonFilePath);
                // TODO: Handle exceptions in de-serialisation
                _persistence.LoadSimulationStateFromJson(ref _scheduler, loadedJsonStr);
                break;
            case PersistenceOption.Save:
                // TODO: Handle exceptions in serialisation
                var savedJsonStr = _persistence.SerializeSimulationStateToJson(ref _scheduler);
                // TODO: Handle exceptions in saving to file
                Persistence.SaveJsonToFile(persistenceEvent.jsonFilePath, savedJsonStr);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    #endregion


    public void Run()
    {
        Log.Info("Starting Simulation");
        _scheduler.Prepare();
        while (_scheduler.IsActive) _scheduler.SimulationStep();
        _scheduler.Shutdown();

        // TODO: Remove temporary serialization test
        // var jsonStr = SerializeSimulationStateToJson();
        // if (!string.IsNullOrEmpty(jsonStr))
        //     // SaveJsonToFile("/home/michael/savegame.json", jsonStr);
        //     SaveJsonToFile("c:/Users/WA_MICHA/savegame.json", jsonStr);
        // LoadSimulationStateFromJson(jsonStr);
        // var newJsonStr = SerializeSimulationStateToJson();
        // if (!string.Equals(jsonStr, newJsonStr)) Log.Error("ERROR: SERIALIZATION FAILED");
    }

    public void EnableSerialization()
    {
        _scheduler.AddEventSink(this, EventType.Persistence);
    }

    #region Fields

    private static readonly ILog Log = LogManager.GetLogger(typeof(Simulation));
    private readonly Persistence _persistence = new();
    private Scheduler _scheduler = scheduler;

    #endregion
}