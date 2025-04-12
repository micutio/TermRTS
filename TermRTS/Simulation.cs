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
            evt is not PersistenceEvent persistEvent) return;

        switch (persistEvent.option)
        {
            case PersistenceOption.Load:
                // TODO: Handle exceptions in loading from file
                var loadError =
                    Persistence.LoadJsonFromFile(persistEvent.jsonFilePath, out var loadedJsonStr);
                if (!string.IsNullOrEmpty(loadError))
                {
                    // TODO: Send error message to in-game log!
                }

                // TODO: Handle exceptions in de-serialisation
                var deserializeError =
                    _persistence.LoadSimulationStateFromJson(ref _scheduler, loadedJsonStr);
                if (!string.IsNullOrEmpty(deserializeError))
                    // TODO: Send error message to in-game log!
                    return;

                break;
            case PersistenceOption.Save:
                // TODO: Handle exceptions in serialisation
                var serializeError =
                    _persistence.SerializeSimulationStateToJson(ref _scheduler,
                        out var savedJsonStr);
                if (!string.IsNullOrEmpty(serializeError))
                    // TODO: Send error message to in-game log!
                    return;

                // TODO: Handle exceptions in saving to file
                var saveError = Persistence.SaveJsonToFile(persistEvent.jsonFilePath, savedJsonStr);
                if (!string.IsNullOrEmpty(saveError))
                    // TODO: Send error message to in-game log!
                    return;

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