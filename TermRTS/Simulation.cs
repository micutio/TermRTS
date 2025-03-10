using System.Text.Json;
using log4net;
using TermRTS.Serialization;

namespace TermRTS;

/// <summary>
/// Top-level class representing a simulation or game.
/// Main purpose is to encapsulate the Scheduler and Core to allow for convenient de/serialisation.
///
/// TODO: Error handling and visible feedback problem solution for user.
/// 
/// See https://madhawapolkotuwa.medium.com/mastering-json-serialization-in-c-with-system-text-json-01f4cec0440d
/// </summary>
public class Simulation(Scheduler scheduler)
{
    #region Private Fields
    
    private static readonly ILog Log = LogManager.GetLogger(typeof(Simulation));
    
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true,
        Converters =
        {
            // handle all subclasses of various interfaces and abstract classes
            new BaseClassConverter<ComponentBase>(GetAllSubTypes<ComponentBase>()),
            new BaseClassConverter<IEventSink>(GetAllSubTypes<IEventSink>()),
            new BaseClassConverter<IEvent>(GetAllSubTypes<IEvent>()),
            new BaseClassConverter<ISimSystem>(GetAllSubTypes<ISimSystem>()),
            new BaseClassConverter<IRenderer>(GetAllSubTypes<IRenderer>()),
            // handle all byte[,] matrices
            new ByteArray2DConverter(),
            // handle all bool[,] matrices
            new BooleanArray2DConverter()
        }
    };
    
    #endregion
    
    #region Properties
    
    public Scheduler Scheduler { get; private set; } = scheduler;
    
    #endregion
    
    #region Public Members
    
    public void Start()
    {
        Log.Info("Starting Simulation");
        Scheduler.Prepare();
        while (Scheduler.IsActive) Scheduler.SimulationStep();
        
        Scheduler.Shutdown();
        
        // TODO: Remove temporary serialization test
        var jsonStr = SerializeSimulationStateToJson();
        if (!string.IsNullOrEmpty(jsonStr))
            SaveJsonToFile("/home/michael/savegame.json", jsonStr);
    }
    
    /// <summary>
    /// Persist the current simulation state to the file system.
    /// </summary>
    public string? SerializeSimulationStateToJson()
    {
        string jsonStr;
        
        try
        {
            jsonStr = JsonSerializer.Serialize(Scheduler, _serializerOptions);
            Console.WriteLine(jsonStr);
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
    /// Load a saved simulation state from the file system.
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
    
    public Scheduler? LoadSimulationStateFromJson(string jsonStr)
    {
        if (string.IsNullOrWhiteSpace(jsonStr))
        {
            Log.ErrorFormat("Error reading simulation state: empty json string.");
            Environment.Exit(1);
            return null;
        }
        
        Scheduler? newScheduler;
        try
        {
            newScheduler = JsonSerializer.Deserialize<Scheduler>(jsonStr, _serializerOptions);
        }
        catch (Exception e)
        {
            Log.ErrorFormat("Error parsing simulation state from json: {0}", e);
            Environment.Exit(1);
            return null;
        }
        
        if (newScheduler == null)
        {
            Log.ErrorFormat("Error: scheduler parsed from json is invalid.");
            Environment.Exit(1);
            return null;
        }
        
        return newScheduler;
    }
    
    #endregion
    
    #region Private Members
    
    private static Type[] GetAllSubTypes<TSuperType>()
    {
        var types = AppDomain
            .CurrentDomain
            .GetAssemblies()
            .SelectMany(x => x.GetTypes())
            .Where(x => typeof(TSuperType).IsAssignableFrom(x) && x is { IsInterface: false, IsAbstract: false })
            .ToArray();
        return types;
    }
    
    #endregion
}