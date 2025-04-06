using System.Text.Json;
using log4net;
using TermRTS.Event;
using TermRTS.Serialization;

namespace TermRTS;

public class Persistence
{
    #region Fields

    private static readonly ILog Log = LogManager.GetLogger(typeof(Simulation));

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true,
        Converters =
        {
            // handle all subclasses of various interfaces and abstract classes
            BaseClassConverter.GetForType<ComponentBase>(),
            BaseClassConverter.GetForType<IDoubleBufferedProperty>(),
            BaseClassConverter.GetForType<IEventSink>(),
            BaseClassConverter.GetForType<IEvent>(),
            BaseClassConverter.GetForType<ISimSystem>(),
            BaseClassConverter.GetForType<IRenderer>(),
            // handle all byte[,] matrices
            new ByteArray2DConverter(),
            // handle all bool[,] matrices
            new BooleanArray2DConverter()
        }
    };

    #endregion

    #region Public Members

    /// <summary>
    ///     Persist the current simulation state to the file system.
    /// </summary>
    public string? SerializeSimulationStateToJson(ref Scheduler scheduler)
    {
        string jsonStr;

        try
        {
            jsonStr = JsonSerializer.Serialize(scheduler.GetSchedulerState(), _serializerOptions);
        }
        catch (Exception e)
        {
            Log.ErrorFormat("Error serializing simulation state to json: {0}", e);
            return null;
        }

        return jsonStr;
    }

    public void LoadSimulationStateFromJson(ref Scheduler scheduler, string? jsonStr)
    {
        if (string.IsNullOrWhiteSpace(jsonStr))
        {
            Log.ErrorFormat("Error reading simulation state: empty json string.");
            Environment.Exit(1);
            return;
        }

        SchedulerState? newSchedulerState;
        try
        {
            newSchedulerState = JsonSerializer.Deserialize<SchedulerState>(jsonStr, _serializerOptions);
        }
        catch (Exception e)
        {
            Log.ErrorFormat("Error parsing simulation state from json: {0}", e);
            Environment.Exit(1);
            return;
        }

        if (newSchedulerState != null)
        {
            scheduler.ReplaceSchedulerState(newSchedulerState);
            return;
        }

        Log.ErrorFormat("Error: simulation state parsed from json is invalid.");
        Environment.Exit(1);
    }

    #endregion

    #region Internal Members

    internal static void SaveJsonToFile(string filePath, string jsonStr)
    {
        try
        {
            File.WriteAllText(filePath, jsonStr);
        }
        catch (Exception e)
        {
            Log.ErrorFormat("Error writing simulation state to file {0}: {1}", filePath, e);
        }
    }

    /// <summary>
    ///     Load a saved simulation state from the file system.
    /// </summary>
    /// TODO: Any better way to handle failed loading?
    /// <param name="filePath"></param>
    internal static string? LoadJsonFromFile(string filePath)
    {
        string jsonStr;
        try
        {
            jsonStr = File.ReadAllText(filePath);
        }
        catch (Exception e)
        {
            Log.ErrorFormat("Error loading simulation state from file {0}: {1}", filePath, e);
            Environment.Exit(1);
            return null;
        }

        return jsonStr;
    }

    #endregion
}