using TermRTS.Event;

namespace TermRTS;

public interface IEventSink
{
    public void ProcessEvent(IEvent evt);
}