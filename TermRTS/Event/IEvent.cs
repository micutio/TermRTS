using System.Text.Json.Serialization;

namespace TermRTS.Event;

public interface IEvent
{
    [JsonIgnore]
    Type EvtType { get; }

    ulong TriggerTime { get; }
}