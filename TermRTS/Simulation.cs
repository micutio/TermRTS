using System.Threading.Channels;
using log4net;
using TermRTS.Event;

namespace TermRTS;

/// <summary>
///     Top-level class representing a simulation or game.
///     Main purpose is to encapsulate the Scheduler and Core to allow for convenient de/serialisation.
///     See link below:
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

    private ChannelReader<ScheduledEvent> LogOutputChannel => _logOutputChannel.Reader;

    #endregion

    #region IEventSink Members

    public void ProcessEvent(IEvent evt)
    {
        if (evt is not Event<Persist>(var (persistOption, filePath)))
            return;

        switch (persistOption)
        {
            case PersistenceOption.Load:
                var isLoadSuccess =
                    Persistence
                        .LoadJsonFromFile(out var loadedJsonStr, filePath, out var loadResponse);
                _logOutputChannel
                    .Writer
                    .TryWrite(ScheduledEvent.From(new SystemLog(loadResponse)));
                if (!isLoadSuccess) break;

                _persistence.GetSimStateFromJson(ref _scheduler, loadedJsonStr,
                    out var getResponse);
                _logOutputChannel
                    .Writer
                    .TryWrite(ScheduledEvent.From(new SystemLog(getResponse)));
                break;

            case PersistenceOption.Save:
                var isSerializeSuccess =
                    _persistence.PutSimStateToJson(
                        ref _scheduler,
                        out var savedJsonStr,
                        out var putResponse);
                _logOutputChannel
                    .Writer
                    .TryWrite(ScheduledEvent.From(new SystemLog(putResponse)));
                if (!isSerializeSuccess) break;

                Persistence.SaveJsonToFile(savedJsonStr, filePath, out var saveResponse);
                if (!string.IsNullOrEmpty(saveResponse))
                    _logOutputChannel
                        .Writer
                        .TryWrite(ScheduledEvent.From(new SystemLog(saveResponse)));
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
        _scheduler.AddEventSources(LogOutputChannel);
    }

    #endregion
}