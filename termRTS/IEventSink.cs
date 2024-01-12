namespace termRTS;

public interface IEventSink
{
    public void ProcessEvent(termRTS.IEvent evt);
}

