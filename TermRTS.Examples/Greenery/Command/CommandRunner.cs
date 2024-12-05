using System.Threading.Channels;
using TermRTS.Examples.Greenery.Event;

namespace TermRTS.Examples.Greenery.Command;

public class CommandRunner : IEventSink
{
    // Replies
    private const string ErrorEmptyCmd = "< Cannot run empty command";
    private const string ErrorNoIdentifier = "< Command must start with an identifier!";
    private const string ErrorTooManyArgs = "< Too many arguments!";
    private const string ErrorUnknownCmd = "< Unknown command!";
    
    // Available commands
    private const string CmdRender = "render";
    private const string SubCmdRenderElevationColor = "elevation_col";
    private const string SubCmdRenderElevationMonochrome = "elevation_mono";
    private const string SubCmdRenderElevationHeatmap = "elevation_heat";
    private const string SubCmdRenderTerrainColor = "terrain_col";
    private const string SubCmdRenderTerrainMonochrome = "terrain_mono";
    private const string SubCmdRenderReliefColor = "relief_col";
    private const string SubCmdRenderReliefMonochrome = "relief_mono";
    
    private readonly Channel<(IEvent, ulong)> _channel = Channel.CreateUnbounded<(IEvent, ulong)>();
    
    public ChannelReader<(IEvent, ulong)> CommandEventReader => _channel.Reader;
    
    public void ProcessEvent(IEvent evt)
    {
        if (evt.Type() == EventType.Custom && evt is CommandEvent cmdEvt) Run(new Scanner(cmdEvt.Command).ScanTokens());
    }
    
    // TODO: Create a notification system that can display the responses
    private string Run(IReadOnlyList<Token> cmdTokens)
    {
        if (cmdTokens.Count == 0) return ErrorEmptyCmd;
        
        if (cmdTokens[0].TokenType != TokenType.Identifier) return ErrorNoIdentifier;
        
        return cmdTokens[0].Lexeme switch
        {
            CmdRender => CommandRenderMode(cmdTokens),
            _ => ErrorUnknownCmd
        };
    }
    
    // TODO: Change argument to listview
    private string CommandRenderMode(IReadOnlyList<Token> tokens)
    {
        if (tokens.Count > 2) return ErrorTooManyArgs;
        switch (tokens[1].Lexeme)
        {
            case SubCmdRenderElevationColor:
                _channel.Writer.TryWrite((new RenderOptionEvent(RenderMode.ElevationColor), 0L));
                break;
            case SubCmdRenderElevationMonochrome:
                _channel.Writer.TryWrite((new RenderOptionEvent(RenderMode.ElevationMonochrome), 0L));
                break;
            case SubCmdRenderElevationHeatmap:
                _channel.Writer.TryWrite((new RenderOptionEvent(RenderMode.ElevationHeatmap), 0L));
                break;
            case SubCmdRenderTerrainColor:
                _channel.Writer.TryWrite((new RenderOptionEvent(RenderMode.TerrainColor), 0L));
                break;
            case SubCmdRenderTerrainMonochrome:
                _channel.Writer.TryWrite((new RenderOptionEvent(RenderMode.TerrainMonochrome), 0L));
                break;
            case SubCmdRenderReliefColor:
                _channel.Writer.TryWrite((new RenderOptionEvent(RenderMode.ReliefColor), 0L));
                break;
            case SubCmdRenderReliefMonochrome:
                _channel.Writer.TryWrite((new RenderOptionEvent(RenderMode.ReliefMonochrome), 0L));
                break;
            default: return ErrorUnknownCmd + tokens[1].Lexeme;
        }
        
        return string.Empty;
    }
}