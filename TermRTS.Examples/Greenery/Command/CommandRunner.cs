namespace TermRTS.Examples.Greenery.Command;

public class CommandRunner
{
    private const string ErrorEmptyCmd = "< Cannot run empty command";
    private const string ErrorNoIdentifier = "< Command must start with an identifier!";
    private const string ErrorUnknownCmd = "< Unknown command!";
    
    public string Run(IReadOnlyList<Token> cmdTokens)
    {
        if (cmdTokens.Count == 0) return ErrorEmptyCmd;
        
        if (cmdTokens[0].TokenType != TokenType.Identifier) return ErrorNoIdentifier;
        
        return cmdTokens[0].Lexeme switch
        {
            
        }
    }
}