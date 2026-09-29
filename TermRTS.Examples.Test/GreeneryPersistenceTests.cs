using System.Numerics;
using TermRTS.Event;
using TermRTS.Examples.Greenery;
using TermRTS.Examples.Greenery.Command;
using TermRTS.Examples.Greenery.Ecs.Component;
using TermRTS.Examples.Greenery.Event;
using Xunit;

namespace TermRTS.Examples.Test;

public class GreeneryPersistenceTests
{
    [Fact]
    public void SaveAndLoad_RestoresGreeneryComponentsAndEvents()
    {
        var core = new Core();
        var drone = new DroneComponent(7, new Vector2(2, 3))
        {
            Path = [new Vector2(2, 3), new Vector2(4, 5)],
            PathIndex = 1
        };
        var fov = new FovChunk(8, 1, 2);
        core.AddNewComponent(drone);
        core.AddNewComponent(fov);
        var scheduler = new Scheduler(core);
        scheduler.FutureEvents.EnqueueEvent(
            ScheduledEvent.From(new Move(7, new Vector2(9, 10)), 35UL));
        ICommand pendingCommand = new GoCommand { X = 11, Y = 12 };
        scheduler.FutureEvents.EnqueueEvent(ScheduledEvent.From(pendingCommand, 36UL));
        var persistence = new Persistence(GreeneryJsonContext.CreatePersistenceTypeRegistry());

        Assert.True(
            persistence.PutSimStateToJson(scheduler, out var json, out var saveResponse),
            saveResponse);

        var restoredScheduler = new Scheduler(new Core());
        Assert.True(
            persistence.GetSimStateFromJson(restoredScheduler, json, out var loadResponse),
            loadResponse);

        Assert.True(
            persistence.PutSimStateToJson(restoredScheduler, out var restoredJson,
                out var resaveResponse),
            resaveResponse);
        Assert.Equal(json, restoredJson);
        Assert.Contains("greenery.drone", json, StringComparison.Ordinal);
        Assert.Contains("greenery.fov-chunk", json, StringComparison.Ordinal);
        Assert.Contains("greenery.move", json, StringComparison.Ordinal);
    }
}