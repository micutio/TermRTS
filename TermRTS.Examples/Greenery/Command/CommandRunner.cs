namespace TermRTS.Examples.Greenery.Command;

public class CommandRunner
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
    private const string SubCmdRenderProfile = "profile";
    
    public string Run(IReadOnlyList<Token> cmdTokens)
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
        switch (tokens[1].Literal)
        {
            case SubCmdRenderElevationColor: throw new NotImplementedException();
            case SubCmdRenderElevationMonochrome: throw new NotImplementedException();
            case SubCmdRenderTerrainColor: throw new NotImplementedException();
            case SubCmdRenderTerrainMonochrome: throw new NotImplementedException();
            case SubCmdRenderProfile: throw new NotImplementedException();
        }
    }
}