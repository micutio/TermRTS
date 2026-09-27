using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using TermRTS.Ecs;
using TermRTS.Event;
using TermRTS.Serialization;

namespace TermRTS.Test;

public class SerializationTest
{
    [Fact]
    public void TestSchedulerSerialization()
    {
        var scheduler = new Scheduler(new Core
        {
            Renderer = new NullRenderer()
        });
        var persistence = new Persistence();

        var putSuccess1 =
            persistence.PutSimStateToJson(ref scheduler, out var expectedJsonStr, out _);

        Assert.True(putSuccess1);
        Assert.NotNull(expectedJsonStr);

        persistence.GetSimStateFromJson(ref scheduler, expectedJsonStr, out _);

        var putSuccess2 =
            persistence.PutSimStateToJson(ref scheduler, out var actualJsonStr, out _);

        Assert.True(putSuccess2);
        Assert.NotNull(actualJsonStr);
        Assert.Equal(expectedJsonStr, actualJsonStr);
    }

    [Fact]
    public void TestSchedulerSerializationWithQueuedEvent()
    {
        var scheduler = new Scheduler(new Core
        {
            Renderer = new NullRenderer()
        });
        scheduler.FutureEvents.EnqueueEvent(
            ScheduledEvent.From(new Persist(PersistenceOption.Save, "state.json"), 42UL));
        var persistence = new Persistence();

        var success = persistence.PutSimStateToJson(ref scheduler, out var json, out var response);

        Assert.True(success, response);
        Assert.NotNull(json);
        Assert.Contains("JsonFilePath", json);
        Assert.Contains("termrts.persist", json);
        Assert.DoesNotContain("EvtType", json);
    }

    [Fact]
    public void TestRegisteredGameTypesRoundTrip()
    {
        var core = new Core();
        core.AddNewComponent(new PersistenceTestComponent(7, "saved component"));
        var scheduler = new Scheduler(core);
        scheduler.FutureEvents.EnqueueEvent(
            ScheduledEvent.From(new PersistenceTestEvent("saved event"), 42UL));
        var registry = new PersistenceTypeRegistry()
            .RegisterComponent<PersistenceTestComponent>(
                "test.component.v1", PersistenceTestJsonContext.Default)
            .RegisterEvent<PersistenceTestEvent>(
                "test.event.v1", PersistenceTestJsonContext.Default);
        var persistence = new Persistence(registry);

        Assert.True(
            persistence.PutSimStateToJson(ref scheduler, out var json, out var saveResponse),
            saveResponse);
        Assert.NotNull(json);

        var restoredScheduler = new Scheduler(new Core());
        Assert.True(
            persistence.GetSimStateFromJson(ref restoredScheduler, json, out var loadResponse),
            loadResponse);

        var restoredState = restoredScheduler.GetSchedulerState();
        var restoredComponent = Assert.IsType<PersistenceTestComponent>(
            Assert.Single(restoredState.CoreState.NewComponents));
        Assert.Equal("saved component", restoredComponent.Label);
        var restoredEvent = Assert.IsType<Event<PersistenceTestEvent>>(
            Assert.Single(restoredState.EventQueueItems).Item1);
        Assert.Equal("saved event", restoredEvent.Payload.Message);
        Assert.Equal(42UL, restoredEvent.TriggerTime);
    }

    [Fact]
    public void TestUnsupportedSnapshotVersionIsRejected()
    {
        var scheduler = new Scheduler(new Core());
        var persistence = new Persistence();
        Assert.True(persistence.PutSimStateToJson(ref scheduler, out var json, out _));
        var snapshot = JsonNode.Parse(json!)!;
        snapshot["FormatVersion"] = 999;

        var success = persistence.GetSimStateFromJson(
            ref scheduler, snapshot.ToJsonString(), out var response);

        Assert.False(success);
        Assert.Contains("version 999", response);
    }

    [Fact]
    public void TestUnregisteredEventTypeCannotBeSaved()
    {
        var scheduler = new Scheduler(new Core());
        scheduler.FutureEvents.EnqueueEvent(ScheduledEvent.From(new PersistenceTestEvent("unregistered")));
        var persistence = new Persistence();

        var success = persistence.PutSimStateToJson(ref scheduler, out _, out var response);

        Assert.False(success);
        Assert.Contains("not registered for persistence", response);
    }

    [Fact]
    public void TestUnknownEventDiscriminatorCannotBeLoaded()
    {
        var registry = new PersistenceTypeRegistry()
            .RegisterEvent<PersistenceTestEvent>(
                "test.event.v1", PersistenceTestJsonContext.Default);
        var persistence = new Persistence(registry);
        var sourceScheduler = new Scheduler(new Core());
        sourceScheduler.FutureEvents.EnqueueEvent(
            ScheduledEvent.From(new PersistenceTestEvent("saved event"), 42UL));
        Assert.True(persistence.PutSimStateToJson(ref sourceScheduler, out var json, out _));
        var unregisteredJson = json!.Replace(
            "test.event.v1", "test.unknown.v1", StringComparison.Ordinal);

        var restoredScheduler = new Scheduler(new Core());
        var success = persistence.GetSimStateFromJson(
            ref restoredScheduler, unregisteredJson, out var response);

        Assert.False(success);
        Assert.Contains("Unregistered persistence discriminator", response);
    }
}

public sealed class PersistenceTestComponent(int entityId, string label) : ComponentBase(entityId)
{
    public string Label { get; } = label;
}

public readonly record struct PersistenceTestEvent(string Message);

[JsonSourceGenerationOptions(IncludeFields = true)]
[JsonSerializable(typeof(PersistenceTestComponent))]
[JsonSerializable(typeof(PersistenceTestEvent))]
internal partial class PersistenceTestJsonContext : JsonSerializerContext
{
}