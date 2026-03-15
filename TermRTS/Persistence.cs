using System.Security;
using System.Text.Json;
using log4net;
using TermRTS.Event;
using TermRTS.Serialization;

namespace TermRTS;

public class Persistence
{
    /// <summary>
    ///     Serialize the current simulation state into a json string.
    /// </summary>
    /// <param name="scheduler">
    ///     Reference to the scheduler, of which the state is to be serialized.
    /// </param>
    /// <param name="jsonStr">
    ///     A json representation of the simulation/scheduler state.
    ///     This returns <c>null</c> if the serialization failed.
    /// </param>
    /// <param name="response">
    ///     Either confirmation if successful, or error information if failed.
    /// </param>
    /// <returns>
    ///     <c>true</c> if serialization successful, <c>false</c> otherwise.
    /// </returns>
    public bool PutSimStateToJson(
        ref Scheduler scheduler,
        out string? jsonStr,
        out string response)
    {
        try
        {
            jsonStr = JsonSerializer.Serialize(scheduler.GetSchedulerState(), _serializerOptions);
            response = "sim state serialized to json";
            return true;
        }
        catch (NotSupportedException e)
        {
            Log.ErrorFormat("Error serializing simulation state to json: {0}", e);
            jsonStr = null;
            response = "Error serializing simulation state to json";
            return false;
        }
    }

    /// <summary>
    ///     Deserialize a simulation state from a JSON string.
    /// </summary>
    /// <param name="scheduler">
    ///     Reference to the scheduler, to which to restore the state
    /// </param>
    /// <param name="jsonStr">
    ///     A json representation of the simulation/scheduler state.
    /// </param>
    /// <param name="response">
    ///     Either confirmation if successful, or error information if failed.
    /// </param>
    /// <returns>
    ///     <c>true</c> if deserialization successful, <c>false</c> otherwise.
    /// </returns>
    public bool GetSimStateFromJson(
        ref Scheduler scheduler,
        string? jsonStr,
        out string response
    )
    {
        if (string.IsNullOrWhiteSpace(jsonStr))
        {
            response = $"Error reading simulation state: empty json string: {jsonStr}";
            return false;
        }

        SchedulerState? newSchedulerState;
        try
        {
            newSchedulerState =
                JsonSerializer.Deserialize<SchedulerState>(jsonStr, _serializerOptions);
            if (newSchedulerState != null)
            {
                scheduler.ReplaceSchedulerState(newSchedulerState);
                response = "sim state deserialized from json";
                return true;
            }

            Log.ErrorFormat("Error parsing simulation state from NULL json: {0}", jsonStr);
            response = "Error: simulation state parsed from json is invalid.";
        }
        catch (ArgumentNullException e)
        {
            Log.ErrorFormat(
                "Error parsing simulation state from null json: {0}\ncaused by jsonStr={1}",
                e,
                jsonStr);
            response = "Error parsing simulation state from null json";
        }
        catch (JsonException e)
        {
            Log.ErrorFormat(
                "Error parsing simulation state from invalid json: {0}\ncaused by jsonStr={1}",
                e,
                jsonStr);
            response = "Error parsing simulation state from invalid json";
        }
        catch (NotSupportedException e)
        {
            Log.ErrorFormat(
                "Error parsing simulation state from json: {0}\ncaused by jsonStr={1}",
                e,
                jsonStr);
            response =
                $"Error parsing simulation state from incompatible json: {e.Message} {jsonStr}";
        }

        return false;
    }

    /// <summary>
    ///     Save a json string to the file system.
    /// </summary>
    /// <param name="jsonStr">
    ///     A json representation of the simulation/scheduler state.
    /// </param>
    /// <param name="filePath">Path to the json file to save to.</param>
    /// <param name="response">
    ///     Either confirmation if successful, or error information if failed.
    /// </param>
    /// <returns>
    ///     <c>true</c> if saving successful, <c>false</c> otherwise.
    /// </returns>
    internal static bool SaveJsonToFile(string? jsonStr, string filePath, out string response)
    {
        if (string.IsNullOrEmpty(jsonStr))
        {
            response = "Cannot save: JSON string is null or empty.";
            return false;
        }

        try
        {
            File.WriteAllText(filePath, jsonStr);
            response = "sim state saved to file";
            return true;
        }
        catch (ArgumentNullException e)
        {
            Log.ErrorFormat(
                "Error writing json to file, invalid path: {0},\ncaused by {1}",
                e,
                filePath);
            response = $"File path is null: {filePath}";
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
            response = "Invalid file path for storing simulation state.";
        }
        catch (PathTooLongException e)
        {
            Log.ErrorFormat("File path is too long: {0}\ncaused by {1}", e, filePath);
            response = "File path is too long";
        }
        catch (DirectoryNotFoundException e)
        {
            Log.ErrorFormat("Directory not found: {0}\ncaused by {1}", e, filePath);
            response = "File path is not a valid directory";
        }
        catch (FileNotFoundException e)
        {
            Log.ErrorFormat("File not found: {0}\ncaused by {1}", e, filePath);
            response = "File does not exist";
        }
        catch (IOException e)
        {
            Log.ErrorFormat("IOException: {0}\ncaused by {1}", e, filePath);
            response = "Error writing simulation state to file";
        }
        catch (UnauthorizedAccessException e)
        {
            Log.ErrorFormat("Invalid access to file: {0}\ncaused by {1}", e, filePath);
            response = $"Invalid user rights to access file path: {filePath}";
        }
        catch (SecurityException e)
        {
            Log.ErrorFormat("{0}\ncaused by {1}", e, filePath);
            response = $"Security error accessing file path: {filePath}";
        }
        catch (NotSupportedException e)
        {
            Log.ErrorFormat("{0}\ncaused by {1}", e, filePath);
            response = $"File is not supported: {filePath}";
        }

        return false;
    }

    /// <summary>
    ///     Load a saved simulation state from the file system into a json string.
    /// </summary>
    /// <param name="jsonStr">
    ///     A json representation of the simulation/scheduler state.
    /// </param>
    /// <param name="filePath">Path to the json file to load from.</param>
    /// <param name="response">
    ///     Either confirmation if successful, or error information if failed.
    /// </param>
    /// <returns>
    ///     <c>true</c> if loading successful, <c>false</c> otherwise.
    /// </returns>
    internal static bool LoadJsonFromFile(out string? jsonStr, string filePath, out string response)
    {
        jsonStr = null;
        try
        {
            jsonStr = File.ReadAllText(filePath);
            response = "sim state loaded from file";
            return true;
        }
        catch (ArgumentNullException e)
        {
            Log.ErrorFormat("Error reading json from file, invalid path: {0},\ncaused by {1}", e,
                filePath);
            response = $"File path is null: {filePath}";
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
            response = "Invalid file path for storing simulation state.";
        }
        catch (PathTooLongException e)
        {
            Log.ErrorFormat("File path is too long: {0}\ncaused by {1}", e, filePath);
            response = "File path is too long";
        }
        catch (DirectoryNotFoundException e)
        {
            Log.ErrorFormat("Directory not found: {0}\ncaused by {1}", e, filePath);
            response = "File path is not a valid directory";
        }
        catch (FileNotFoundException e)
        {
            Log.ErrorFormat("File not found: {0}\ncaused by {1}", e, filePath);
            response = "File does not exist";
        }
        catch (IOException e)
        {
            Log.ErrorFormat("IOException: {0}\ncaused by {1}", e, filePath);
            response = "Error writing simulation state to file";
        }
        catch (UnauthorizedAccessException e)
        {
            Log.ErrorFormat("Invalid access to file: {0}\ncaused by {1}", e, filePath);
            response = $"Invalid user rights to access file path: {filePath}";
        }
        catch (SecurityException e)
        {
            Log.ErrorFormat("{0}\ncaused by {1}", e, filePath);
            response = $"Security error accessing file path: {filePath}";
        }
        catch (NotSupportedException e)
        {
            Log.ErrorFormat("{0}\ncaused by {1}", e, filePath);
            response = $"File is not supported: {filePath}";
        }

        return false;
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