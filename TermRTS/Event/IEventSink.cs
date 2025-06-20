namespace TermRTS.Event;

public interface IEventSink
{
    public void ProcessEvent(IEvent evt);
}