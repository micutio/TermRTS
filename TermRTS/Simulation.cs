using Microsoft.Extensions.Logging;
using TermRTS.Event;
using TermRTS.Log;
using TermRTS.Serialization;

namespace TermRTS;

/// <summary>
///     Top-level class representing a simulation or game.
///     Main purpose is to encapsulate the Scheduler and Core to allow for convenient de/serialisation.
///     See link below:
///     https://madhawapolkotuwa.medium.com/mastering-json-serialization-in-c-with-system-text-json-01f4cec0440d
/// </summary>
public class Simulation(Scheduler scheduler, PersistenceTypeRegistry? typeRegistry = null)
    : IEventSink
{
    #region IEventSink Members

    public void ProcessEvent(IEvent evt)
    {
        if (evt is not Event<Persist>(var persistEvent))
            return;

        RequiredPersistAction = persistEvent;
    }

    #endregion

    #region Properties

    public bool IsSystemLogEnabled { get; set; }
    private Persist? RequiredPersistAction { get; set; }

    #endregion

    #region Fields

    private static ILogger<Simulation> Log => TermRtsLog.For<Simulation>();
    private readonly Persistence _persistence = new(typeRegistry);
    private readonly Scheduler _scheduler = scheduler;

    #endregion

    #region Public Members

    public void Run()
    {
        Log.LogInformation("Starting Simulation");
        _scheduler.Prepare();
        while (_scheduler.IsActive)
        {
            _scheduler.SimulationStep();
            CheckPendingPersistAction();
        }
    }

    public void EnableSerialization()
    {
        _scheduler.AddEventSink(this, typeof(Persist));
    }

    #endregion

    #region Private Methods

    private void CheckPendingPersistAction()
    {
        if (RequiredPersistAction is not var (option, filePath)) return;

        switch (option)
        {
            case PersistenceOption.Load:
                LoadState(filePath);
                break;

            case PersistenceOption.Save:
                SaveState(filePath);
                break;
            default:
                // TODO: Log to system and user instead.
                throw new ArgumentOutOfRangeException();
        }

        RequiredPersistAction = null;
    }

    private void LoadState(string filePath)
    {
        var isLoadSuccess =
            Persistence
                .LoadJsonFromFile(
                    out var loadedJsonStr,
                    filePath,
                    out var loadResponse);

        if (IsSystemLogEnabled)
            _scheduler.FutureEvents.EnqueueEvent(
                ScheduledEvent.From(new SystemLog(loadResponse)));

        if (!isLoadSuccess) return;

        _persistence.GetSimStateFromJson(_scheduler, loadedJsonStr, out var getResponse);

        if (IsSystemLogEnabled)
            _scheduler.FutureEvents.EnqueueEvent(
                ScheduledEvent.From(new SystemLog(getResponse)));
    }

    private void SaveState(string filePath)
    {
        var isSerializeSuccess =
            _persistence.PutSimStateToJson(
                _scheduler,
                out var savedJsonStr,
                out var putResponse);
        if (IsSystemLogEnabled)
            _scheduler.FutureEvents.EnqueueEvent(
                ScheduledEvent.From(new SystemLog(putResponse)));
        if (!isSerializeSuccess) return;

        Persistence.SaveJsonToFile(savedJsonStr, filePath, out var saveResponse);

        if (!string.IsNullOrEmpty(saveResponse) && IsSystemLogEnabled)
            _scheduler.FutureEvents.EnqueueEvent(ScheduledEvent.From(new SystemLog(saveResponse)));
    }

    #endregion
}