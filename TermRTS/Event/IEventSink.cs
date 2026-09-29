namespace TermRTS.Event;

public interface IEventSink
{
    void ProcessEvent(IEvent evt);
}