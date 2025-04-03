using System.Text.Json;
using log4net;
using TermRTS.Serialization;

namespace TermRTS;

/// <summary>
///     Top-level class representing a simulation or game.
///     Main purpose is to encapsulate the Scheduler and Core to allow for convenient de/serialisation.
///     TODO: Error handling and visible feedback problem solution for user.
///     See https://madhawapolkotuwa.medium.com/mastering-json-serialization-in-c-with-system-text-json-01f4cec0440d
/// </summary>
public class Simulation(Scheduler scheduler)
{
    #region Properties

    public Scheduler Scheduler { get; } = scheduler;

    #endregion

    public void Run()
    {
        Log.Info("Starting Simulation");
        Scheduler.Prepare();
        while (Scheduler.IsActive) Scheduler.SimulationStep();
        Scheduler.Shutdown();

        // TODO: Remove temporary serialization test
        var jsonStr = SerializeSimulationStateToJson();
        if (!string.IsNullOrEmpty(jsonStr))
            // SaveJsonToFile("/home/michael/savegame.json", jsonStr);
            SaveJsonToFile("c:/Users/WA_MICHA/savegame.json", jsonStr);
        LoadSimulationStateFromJson(jsonStr);
        var newJsonStr = SerializeSimulationStateToJson();
        if (!string.Equals(jsonStr, newJsonStr)) Log.Error("ERROR: SERIALIZATION FAILED");
    }

    /// <summary>
    ///     Persist the current simulation state to the file system.
    /// </summary>
    public string? SerializeSimulationStateToJson()
    {
        string jsonStr;

        try
        {
            jsonStr = JsonSerializer.Serialize(Scheduler.GetSchedulerState(), _serializerOptions);
        }
        catch (Exception e)
        {
            Log.ErrorFormat("Error serializing simulation state to json: {0}", e);
            return null;
        }

        return jsonStr;
    }

    private static void SaveJsonToFile(string filePath, string jsonStr)
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
    private static string? LoadJsonFromFile(string filePath)
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

    public void LoadSimulationStateFromJson(string jsonStr)
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
            Scheduler.ReplaceSchedulerState(newSchedulerState);
            return;
        }

        Log.ErrorFormat("Error: simulation state parsed from json is invalid.");
        Environment.Exit(1);
    }

    #region Private Fields

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
}