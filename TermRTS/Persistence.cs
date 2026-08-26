using System.Security;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TermRTS.Event;
using TermRTS.Log;
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
            jsonStr = JsonSerializer.Serialize(
                scheduler.GetSchedulerState(),
                TermRTSJsonContext.Default.SchedulerState);
            response = "sim state serialized to json";
            return true;
        }
        catch (Exception e) when (e is NotSupportedException or JsonException)
        {
            Log.LogError(e, "Error serializing simulation state to json");
            jsonStr = null;
            response = $"Error serializing simulation state to json: {e.Message}";
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
                JsonSerializer.Deserialize(jsonStr, TermRTSJsonContext.Default.SchedulerState);
            if (newSchedulerState != null)
            {
                scheduler.ReplaceSchedulerState(newSchedulerState);
                response = "sim state deserialized from json";
                Console.WriteLine($"GetSimStateFromJson response: {response}");
                return true;
            }

            Log.LogError("Error parsing simulation state from NULL json: {Json}", jsonStr);
            response = "Error: simulation state parsed from json is invalid.";
        }
        catch (ArgumentNullException e)
        {
            Log.LogError(e, "Error parsing simulation state from null json: {Json}", jsonStr);
            response = "Error parsing simulation state from null json";
        }
        catch (JsonException e)
        {
            Log.LogError(e, "Error parsing simulation state from invalid json: {Json}", jsonStr);
            response = "Error parsing simulation state from invalid json";
        }
        catch (NotSupportedException e)
        {
            Log.LogError(e, "Error parsing simulation state from json: {Json}", jsonStr);
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
            Log.LogError(e, "Error writing json to file, invalid path: {FilePath}", filePath);
            response = $"File path is null: {filePath}";
        }
        catch (ArgumentException e)
        {
            Log.LogError(e,
                "File path is either too short or contains invalid characters: {FilePath}. Invalid characters are: {InvalidChars}",
                filePath,
                Path.GetInvalidFileNameChars());
            response = "Invalid file path for storing simulation state.";
        }
        catch (PathTooLongException e)
        {
            Log.LogError(e, "File path is too long: {FilePath}", filePath);
            response = "File path is too long";
        }
        catch (DirectoryNotFoundException e)
        {
            Log.LogError(e, "Directory not found: {FilePath}", filePath);
            response = "File path is not a valid directory";
        }
        catch (FileNotFoundException e)
        {
            Log.LogError(e, "File not found: {FilePath}", filePath);
            response = "File does not exist";
        }
        catch (IOException e)
        {
            Log.LogError(e, "IOException writing simulation state: {FilePath}", filePath);
            response = "Error writing simulation state to file";
        }
        catch (UnauthorizedAccessException e)
        {
            Log.LogError(e, "Invalid access to file: {FilePath}", filePath);
            response = $"Invalid user rights to access file path: {filePath}";
        }
        catch (SecurityException e)
        {
            Log.LogError(e, "Security error accessing file path: {FilePath}", filePath);
            response = $"Security error accessing file path: {filePath}";
        }
        catch (NotSupportedException e)
        {
            Log.LogError(e, "File is not supported: {FilePath}", filePath);
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
            Log.LogError(e, "Error reading json from file, invalid path: {FilePath}", filePath);
            response = $"File path is null: {filePath}";
        }
        catch (ArgumentException e)
        {
            Log.LogError(e,
                "File path is either too short or contains invalid characters: {FilePath}. Invalid characters are: {InvalidChars}",
                filePath,
                Path.GetInvalidFileNameChars());
            response = "Invalid file path for storing simulation state.";
        }
        catch (PathTooLongException e)
        {
            Log.LogError(e, "File path is too long: {FilePath}", filePath);
            response = "File path is too long";
        }
        catch (DirectoryNotFoundException e)
        {
            Log.LogError(e, "Directory not found: {FilePath}", filePath);
            response = "File path is not a valid directory";
        }
        catch (FileNotFoundException e)
        {
            Log.LogError(e, "File not found: {FilePath}", filePath);
            response = "File does not exist";
        }
        catch (IOException e)
        {
            Log.LogError(e, "IOException reading simulation state: {FilePath}", filePath);
            response = "Error writing simulation state to file";
        }
        catch (UnauthorizedAccessException e)
        {
            Log.LogError(e, "Invalid access to file: {FilePath}", filePath);
            response = $"Invalid user rights to access file path: {filePath}";
        }
        catch (SecurityException e)
        {
            Log.LogError(e, "Security error accessing file path: {FilePath}", filePath);
            response = $"Security error accessing file path: {filePath}";
        }
        catch (NotSupportedException e)
        {
            Log.LogError(e, "File is not supported: {FilePath}", filePath);
            response = $"File is not supported: {filePath}";
        }

        return false;
    }

    #region Fields

    private static ILogger<Persistence> Log => TermRtsLog.For<Persistence>();

    #endregion
}