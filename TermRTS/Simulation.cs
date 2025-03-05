using System.Text.Json;
using log4net;
using TermRTS.Serialization;

namespace TermRTS;

/// <summary>
/// Top-level class representing a simulation or game.
/// Main purpose is to encapsulate the Scheduler and Core to allow for convenient de/serialisation.
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
        Converters = { new BaseClassConverter<ComponentBase>(GetAllComponentTypes()) }
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
        Save("C:\\Users\\WA_MICHA\\savegame.json");
    }
    
    /// <summary>
    /// Persist the current simulation state to the file system.
    /// </summary>
    public void Save(string fileName)
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
            return;
        }
        
        try
        {
            File.WriteAllText(fileName, jsonStr);
        }
        catch (Exception e)
        {
            Log.ErrorFormat("Error writing simulation state to file {0}: {1}", fileName, e);
        }
    }
    
    /// <summary>
    /// Load a saved simulation state from the file system.
    /// </summary>
    /// TODO: Any better way to handle failed loading?
    /// <param name="fileName"></param>
    public void Load(string fileName)
    {
        string jsonStr;
        try
        {
            jsonStr = File.ReadAllText(fileName);
        }
        catch (Exception e)
        {
            Log.ErrorFormat("Error loading simulation state from file {0}: {1}", fileName, e);
            Environment.Exit(1);
            return;
        }
        
        if (string.IsNullOrWhiteSpace(jsonStr))
        {
            Log.ErrorFormat("Error reading simulation state yielded empty json string.");
            Environment.Exit(1);
            return;
        }
        
        Scheduler? newScheduler;
        try
        {
            newScheduler = JsonSerializer.Deserialize<Scheduler>(jsonStr);
        }
        catch (Exception e)
        {
            Log.ErrorFormat("Error parsing simulation state from json: {0}", e);
            Environment.Exit(1);
            return;
        }
        
        if (newScheduler == null)
        {
            Log.ErrorFormat("Error: scheduler parsed from json is invalid.");
            Environment.Exit(1);
        }
        
        Scheduler = newScheduler;
    }
    
    #endregion
    
    #region Private Members
    
    private static Type[] GetAllComponentTypes()
    {
        var types = AppDomain
            .CurrentDomain
            .GetAssemblies()
            .SelectMany(x => x.GetTypes())
            .Where(x => typeof(ComponentBase).IsAssignableFrom(x) && x is { IsInterface: false, IsAbstract: false })
            .ToArray();
        return types;
    }
    
    #endregion
}