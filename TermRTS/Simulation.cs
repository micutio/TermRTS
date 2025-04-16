using System.Threading.Channels;
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
    #region Private Fields

    private static readonly ILog Log = LogManager.GetLogger(typeof(Simulation));
    private readonly Persistence _persistence = new();
    private Scheduler _scheduler = scheduler;

    private readonly Channel<ScheduledEvent> _logOutputChannel =
        Channel.CreateUnbounded<ScheduledEvent>();

    #endregion


    #region Properties

    public ChannelReader<ScheduledEvent> LogOutputChannel => _logOutputChannel.Reader;

    #endregion

    #region IEventSink Members

    public void ProcessEvent(IEvent evt)
    {
        if (evt is not Event<Persist>(var (persistOption, jsonFilePath)))
            return;

        switch (persistOption)
        {
            case PersistenceOption.Load:
                var loadError = Persistence.LoadJsonFromFile(jsonFilePath, out var loadedJsonStr);
                if (!string.IsNullOrEmpty(loadError))
                    _logOutputChannel
                        .Writer
                        .TryWrite(ScheduledEvent.From(new SystemLog(loadError)));

                var deserializeError =
                    _persistence.LoadSimulationStateFromJson(ref _scheduler, loadedJsonStr);
                if (!string.IsNullOrEmpty(deserializeError))
                    _logOutputChannel
                        .Writer
                        .TryWrite(ScheduledEvent.From(new SystemLog(deserializeError)));
                break;
            case PersistenceOption.Save:
                var serializeError =
                    _persistence
                        .SerializeSimulationStateToJson(ref _scheduler, out var savedJsonStr);
                if (!string.IsNullOrEmpty(serializeError))
                    _logOutputChannel
                        .Writer
                        .TryWrite(ScheduledEvent.From(new SystemLog(serializeError)));

                var saveError =
                    Persistence.SaveJsonToFile(jsonFilePath, savedJsonStr);
                if (!string.IsNullOrEmpty(saveError))
                    _logOutputChannel
                        .Writer
                        .TryWrite(ScheduledEvent.From(new SystemLog(saveError)));
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    #endregion

    #region Public Members

    public void Run()
    {
        Log.Info("Starting Simulation");
        _scheduler.Prepare();
        while (_scheduler.IsActive) _scheduler.SimulationStep();
        _scheduler.Shutdown();
    }

    public void EnableSerialization()
    {
        _scheduler.AddEventSink(this, typeof(Persist));
    }

    public void EnableSystemLog()
    {
        _scheduler.AddEventSink(this, typeof(SystemLog));
    }

    #endregion
}