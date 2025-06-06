using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using TermRTS.Algorithms;
using TermRTS.Event;
using TermRTS.Examples.Greenery.Event;
using TermRTS.Examples.Greenery.Ui;

namespace TermRTS.Examples.Greenery.Command;

public class CommandRunner : IEventSink
{
    // Replies
    private const string ErrorEmptyCmd = "< Cannot run empty command";
    private const string ErrorNoIdentifier = "< Command must start with an identifier!";
    private const string ErrorTooManyArgs = "< Too many arguments!";
    private const string ErrorTooFewArgs = "< Too few arguments!";
    private const string ErrorUnknownCmd = "< Unknown command!";

    // Available commands
    private const string CmdGo = "go";
    private const string CmdRender = "render";
    private const string SubCmdRenderElevationColor = "elevation_col";
    private const string SubCmdRenderElevationMonochrome = "elevation_mono";
    private const string SubCmdRenderHeatmapColor = "heat_col";
    private const string SubCmdRenderHeatmapMonochrome = "heat_mono";

    private const string SubCmdRenderTerrainColor = "terrain_col";
    private const string SubCmdRenderTerrainMonochrome = "terrain_mono";
    private const string SubCmdRenderReliefColor = "relief_col";
    private const string SubCmdRenderReliefMonochrome = "relief_mono";
    private const string SubCmdRenderContourColor = "contour_col";
    private const string SubCmdRenderContourMonochrome = "contour_mono";

    private const string CmdSave = "save";
    private const string CmdLoad = "load";

    private readonly Channel<ScheduledEvent> _channel = Channel.CreateUnbounded<ScheduledEvent>();

    public ChannelReader<ScheduledEvent> CommandEventReader => _channel.Reader;

    #region IEventSink Members

    public void ProcessEvent(IEvent evt)
    {
        if (evt is not Event<Event.Command>(var command)) return;

        Run(new Scanner(command.Cmd).ScanTokens());
    }

    #endregion

    // TODO: Create a notification system that can display the responses
    private string Run(IReadOnlyList<Token> cmdTokens)
    {
        if (cmdTokens.Count == 0) return ErrorEmptyCmd;

        if (cmdTokens[0].TokenType != TokenType.Identifier) return ErrorNoIdentifier;

        return cmdTokens[0].Lexeme switch
        {
            CmdGo => CommandGo(cmdTokens),
            CmdRender => CommandRenderMode(cmdTokens),
            CmdLoad => CommandLoad(),
            CmdSave => CommandSave(),
            _ => ErrorUnknownCmd
        };
    }

    private string CommandGo(IReadOnlyList<Token> tokens)
    {
        if (tokens.Count < 1) return ErrorTooFewArgs;

        if (tokens.Count > 3) return ErrorTooManyArgs;

        if (tokens[1].TokenType != TokenType.Number || tokens[2].TokenType != TokenType.Number)
            return "Error: both following arguments must be numbers";

        var x = Convert.ToSingle(tokens[1].Literal);
        var y = Convert.ToSingle(tokens[2].Literal);

        // TODO: Make EntityId dynamic!
        _channel.Writer.TryWrite(ScheduledEvent.From(new Move(3, new Vector2(x, y))));
        return string.Empty;
    }

    // TODO: Change argument to listview
    private string CommandRenderMode(IReadOnlyList<Token> tokens)
    {
        if (tokens.Count < 1) return ErrorTooFewArgs;

        if (tokens.Count > 2) return ErrorTooManyArgs;

        switch (tokens[1].Lexeme)
        {
            case SubCmdRenderElevationColor:
                _channel.Writer.TryWrite(ScheduledEvent.From(MapRenderMode.ElevationColor));
                break;
            case SubCmdRenderElevationMonochrome:
                _channel.Writer.TryWrite(ScheduledEvent.From(MapRenderMode.ElevationMonochrome));
                break;
            case SubCmdRenderHeatmapColor:
                _channel.Writer.TryWrite(ScheduledEvent.From(MapRenderMode.HeatMapColor));
                break;
            case SubCmdRenderHeatmapMonochrome:
                _channel.Writer.TryWrite(ScheduledEvent.From(MapRenderMode.HeatMapMonochrome));
                break;
            case SubCmdRenderTerrainColor:
                _channel.Writer.TryWrite(ScheduledEvent.From(MapRenderMode.TerrainColor));
                break;
            case SubCmdRenderTerrainMonochrome:
                _channel.Writer.TryWrite(ScheduledEvent.From(MapRenderMode.TerrainMonochrome));
                break;
            case SubCmdRenderReliefColor:
                _channel.Writer.TryWrite(ScheduledEvent.From(MapRenderMode.ReliefColor));
                break;
            case SubCmdRenderReliefMonochrome:
                _channel.Writer.TryWrite(ScheduledEvent.From(MapRenderMode.ReliefMonochrome));
                break;
            case SubCmdRenderContourColor:
                _channel.Writer.TryWrite(ScheduledEvent.From(MapRenderMode.ContourColor));
                break;
            case SubCmdRenderContourMonochrome:
                _channel.Writer.TryWrite(ScheduledEvent.From(MapRenderMode.ContourMonochrome));
                break;
            default: return ErrorUnknownCmd + tokens[1].Lexeme;
        }

        return string.Empty;
    }

    private string CommandSave()
    {
        _channel
            .Writer
            .TryWrite(ScheduledEvent.From(new Persist(PersistenceOption.Save, GetFilePath())));
        return string.Empty;
    }

    private string CommandLoad()
    {
        _channel
            .Writer
            .TryWrite(ScheduledEvent.From(new Persist(PersistenceOption.Load, GetFilePath())));
        return string.Empty;
    }

    private static string GetFilePath()
    {
        // TODO: Use XDG defaults
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "c:/Users/WA_MICHA/savegame.json"
            : "/home/michael/savegame.json";
    }
}