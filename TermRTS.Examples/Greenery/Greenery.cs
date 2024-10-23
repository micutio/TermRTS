using log4net;

namespace TermRTS.Examples.Greenery;

public class Greenery : IRunnableExample
{
    private readonly ILog _log;
    
    public Greenery()
    {
        _log = LogManager.GetLogger(GetType());
    }
    
    public void Run()
    {
        Console.Out.WriteLine("Greenery logging attempts:");
        _log.Info("Greenery app start");
        _log.Debug("Greenery app debug message");
        _log.Error("Greenery app error message");
        _log.Fatal("Greenery app fatal message");
        _log.Info("Greenery app shutdown");
    }
}