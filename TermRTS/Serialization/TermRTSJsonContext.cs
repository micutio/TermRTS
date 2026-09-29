using System.Text.Json.Serialization;
using TermRTS.Ecs;
using TermRTS.Event;

namespace TermRTS.Serialization;

[JsonSourceGenerationOptions(WriteIndented = true, IncludeFields = true)]
[JsonSerializable(typeof(SchedulerState))]
[JsonSerializable(typeof(CoreState))]
[JsonSerializable(typeof(List<ScheduledEvent>))]
[JsonSerializable(typeof(List<(IEvent, ulong)>))]
[JsonSerializable(typeof(List<Entity>))]
[JsonSerializable(typeof(List<ComponentBase>))]
[JsonSerializable(typeof(List<IEvent>))]
[JsonSerializable(typeof(List<IEventSink>))]
[JsonSerializable(typeof(List<ISimSystem>))]
[JsonSerializable(typeof(List<IRenderer>))]
[JsonSerializable(typeof(List<IDoubleBufferedProperty>))]
[JsonSerializable(typeof(List<byte[]>))]
[JsonSerializable(typeof(List<bool[]>))]
[JsonSerializable(typeof(byte[,]))]
[JsonSerializable(typeof(bool[,]))]
[JsonSerializable(typeof(ComponentBase))]
[JsonSerializable(typeof(Entity))]
[JsonSerializable(typeof(ScheduledEvent))]
[JsonSerializable(typeof(Event<Persist>))]
[JsonSerializable(typeof(Event<Shutdown>))]
[JsonSerializable(typeof(Event<SystemLog>))]
[JsonSerializable(typeof(Event<Profile>))]
[JsonSerializable(typeof(Persist))]
[JsonSerializable(typeof(Shutdown))]
[JsonSerializable(typeof(SystemLog))]
[JsonSerializable(typeof(Profile))]
internal partial class TermRTSJsonContext : JsonSerializerContext
{
}