using System.Runtime.Serialization;
using System.Security;
using System.Text.Json;
using log4net;
using TermRTS.Event;
using TermRTS.Serialization;

namespace TermRTS;

public class Persistence
{
    /// <summary>
    ///     Persist the current simulation state to the file system.
    /// </summary>
    public string? SerializeSimulationStateToJson(ref Scheduler scheduler, out string? jsonStr)
    {
        jsonStr = null;
        try
        {
            jsonStr = JsonSerializer.Serialize(scheduler.GetSchedulerState(), _serializerOptions);
        }
        catch (NotSupportedException e)
        {
            Log.ErrorFormat("Error serializing simulation state to json: {0}", e);
            return "Error serializing simulation state to json";
        }

        return null;
    }

    public string? LoadSimulationStateFromJson(ref Scheduler scheduler, string? jsonStr)
    {
        if (string.IsNullOrWhiteSpace(jsonStr))
            return $"Error reading simulation state: empty json string: {jsonStr}";

        SchedulerState? newSchedulerState;
        try
        {
            newSchedulerState =
                JsonSerializer.Deserialize<SchedulerState>(jsonStr, _serializerOptions);
        }
        catch (ArgumentNullException e)
        {
            Log.ErrorFormat(
                "Error parsing simulation state from null json: {0}\ncaused by jsonStr={1}",
                e,
                jsonStr);
            return $"Error parsing simulation state from null json";
        }
        catch (JsonException e)
        {
            Log.ErrorFormat(
                "Error parsing simulation state from invalid json: {0}\ncaused by jsonStr={1}",
                e,
                jsonStr);
            return "Error parsing simulation state from invalid json";
        }
        catch (NotSupportedException e)
        {
            Log.ErrorFormat(
                "Error parsing simulation state from incompatible json: {0}\ncaused by jsonStr={1}",
                e,
                jsonStr);
            return $"Error parsing simulation state from incompatible json: {e.Message} {jsonStr}";
        }

        if (newSchedulerState == null)
        {
            Log.ErrorFormat("Error parsing simulation state from NULL json: {0}", jsonStr);
            return "Error: simulation state parsed from json is invalid.";
        }

        scheduler.ReplaceSchedulerState(newSchedulerState);
        return null;
    }

    /// <summary>
    /// Save a simulation state to the local file system.
    /// </summary>
    /// <param name="filePath">Path to the json file to save to.</param>
    /// <param name="jsonStr">Simulation state in form of a json string.</param>
    /// <returns></returns>
    internal static string? SaveJsonToFile(string filePath, string? jsonStr)
    {
        try
        {
            File.WriteAllText(filePath, jsonStr);
            return null;
        }
        catch (ArgumentNullException e)
        {
            Log.ErrorFormat("Error writing json to file, invalid path: {0},\ncaused by {1}", e,
                filePath);
            return $"File path is null: {filePath}";
        }
        catch (ArgumentException e)
        {
            Log.ErrorFormat(
                "File path is either too short or contains invalid characters: {0}\n" +
                "caused by {1}\n" +
                "invalid characters are: {2}",
                e,
                filePath,
                Path.GetInvalidFileNameChars());
            return "Invalid file path for storing simulation state.";
        }
        catch (PathTooLongException e)
        {
            Log.ErrorFormat("File path is too long: {0}\ncaused by {1}", e, filePath);
            return "File path is too long";
        }
        catch (DirectoryNotFoundException e)
        {
            Log.ErrorFormat("Directory not found: {0}\ncaused by {1}", e, filePath);
            return "File path is not a valid directory";
        }
        catch (FileNotFoundException e)
        {
            Log.ErrorFormat("File not found: {0}\ncaused by {1}", e, filePath);
            return "File does not exist";
        }
        catch (IOException e)
        {
            Log.ErrorFormat("IOException: {0}\ncaused by {1}", e, filePath);
            return "Error writing simulation state to file";
        }
        catch (UnauthorizedAccessException e)
        {
            Log.ErrorFormat("Invalid access to file: {0}\ncaused by {1}", e, filePath);
            return $"Invalid user rights to access file path: {filePath}";
        }
        catch (SecurityException e)
        {
            Log.ErrorFormat("{0}\ncaused by {1}", e, filePath);
            return $"Security error accessing file path: {filePath}";
        }
        catch (NotSupportedException e)
        {
            Log.ErrorFormat("{0}\ncaused by {1}", e, filePath);
            return $"File is not supported: {filePath}";
        }
    }

    /// <summary>
    ///     Load a saved simulation state from the file system.
    /// </summary>
    /// <param name="filePath">Path to the file to load from.</param>
    /// <param name="jsonStr">Simulation state in form of a json string.</param>
    /// <returns>A string with error information if loading fails, null otherwise.</returns>
    internal static string? LoadJsonFromFile(string filePath, out string? jsonStr)
    {
        jsonStr = null;
        try
        {
            jsonStr = File.ReadAllText(filePath);
            return null;
        }
        catch (ArgumentNullException e)
        {
            Log.ErrorFormat("Error reading json from file, invalid path: {0},\ncaused by {1}", e,
                filePath);
            return $"File path is null: {filePath}";
        }
        catch (ArgumentException e)
        {
            Log.ErrorFormat(
                "File path is either too short or contains invalid characters: {0}\n" +
                "caused by {1}\n" +
                "invalid characters are: {2}",
                e,
                filePath,
                Path.GetInvalidFileNameChars());
            return "Invalid file path for storing simulation state.";
        }
        catch (PathTooLongException e)
        {
            Log.ErrorFormat("File path is too long: {0}\ncaused by {1}", e, filePath);
            return "File path is too long";
        }
        catch (DirectoryNotFoundException e)
        {
            Log.ErrorFormat("Directory not found: {0}\ncaused by {1}", e, filePath);
            return "File path is not a valid directory";
        }
        catch (FileNotFoundException e)
        {
            Log.ErrorFormat("File not found: {0}\ncaused by {1}", e, filePath);
            return "File does not exist";
        }
        catch (IOException e)
        {
            Log.ErrorFormat("IOException: {0}\ncaused by {1}", e, filePath);
            return "Error writing simulation state to file";
        }
        catch (UnauthorizedAccessException e)
        {
            Log.ErrorFormat("Invalid access to file: {0}\ncaused by {1}", e, filePath);
            return $"Invalid user rights to access file path: {filePath}";
        }
        catch (SecurityException e)
        {
            Log.ErrorFormat("{0}\ncaused by {1}", e, filePath);
            return $"Security error accessing file path: {filePath}";
        }
        catch (NotSupportedException e)
        {
            Log.ErrorFormat("{0}\ncaused by {1}", e, filePath);
            return $"File is not supported: {filePath}";
        }
    }

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
}