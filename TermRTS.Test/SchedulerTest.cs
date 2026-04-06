using TermRTS.Event;

namespace TermRTS.Test;

public class SchedulerTest
{
    [Fact]
    public void Event_dispatch_delivers_to_sink_registered_for_that_type()
    {
        var core = new Core { Renderer = new NullRenderer() };
        var scheduler = new Scheduler(core);
        var sink = new RecordingSink();
        scheduler.AddEventSink(sink, typeof(Shutdown));

        scheduler.Prepare();
        scheduler.FutureEvents.EnqueueEvent(ScheduledEvent.From(new Shutdown(), 0UL));
        scheduler.SimulationStep();

        Assert.Single(sink.Received);
        Assert.IsType<Event<Shutdown>>(sink.Received[0]);
    }

    [Fact]
    public void Event_dispatch_sends_only_matching_type_to_each_sink()
    {
        var core = new Core { Renderer = new NullRenderer() };
        var scheduler = new Scheduler(core);
        var sinkSystemLog = new RecordingSink();
        var sinkProfile = new RecordingSink();
        scheduler.AddEventSink(sinkSystemLog, typeof(SystemLog));
        scheduler.AddEventSink(sinkProfile, typeof(Profile));

        scheduler.Prepare();
        scheduler.FutureEvents.EnqueueEvent(ScheduledEvent.From(new SystemLog("log"), 0UL));
        scheduler.FutureEvents.EnqueueEvent(ScheduledEvent.From(new Profile("profile"), 0UL));
        scheduler.SimulationStep();

        Assert.Single(sinkSystemLog.Received);
        Assert.IsType<Event<SystemLog>>(sinkSystemLog.Received[0]);
        Assert.Single(sinkProfile.Received);
        Assert.IsType<Event<Profile>>(sinkProfile.Received[0]);
    }

    [Fact]
    public void RemoveEventSink_stops_delivery_to_that_sink()
    {
        var core = new Core { Renderer = new NullRenderer() };
        var scheduler = new Scheduler(core);
        var sink = new RecordingSink();
        scheduler.AddEventSink(sink, typeof(SystemLog));

        scheduler.Prepare();
        scheduler.FutureEvents.EnqueueEvent(ScheduledEvent.From(new SystemLog("first"), 0UL));
        scheduler.SimulationStep();

        Assert.Single(sink.Received);

        scheduler.RemoveEventSink(sink, typeof(SystemLog));
        scheduler.FutureEvents.EnqueueEvent(ScheduledEvent.From(new SystemLog("second"), 0UL));
        scheduler.SimulationStep();

        Assert.Single(sink.Received);
        Assert.IsType<Event<SystemLog>>(sink.Received[0]);
        var payload = ((Event<SystemLog>)sink.Received[0]).Payload;
        Assert.Equal("first", payload.Content);
    }

    [Fact]
    public void Multiple_sinks_for_same_type_all_receive_event()
    {
        var core = new Core { Renderer = new NullRenderer() };
        var scheduler = new Scheduler(core);
        var sink1 = new RecordingSink();
        var sink2 = new RecordingSink();
        scheduler.AddEventSink(sink1, typeof(SystemLog));
        scheduler.AddEventSink(sink2, typeof(SystemLog));

        scheduler.Prepare();
        scheduler.FutureEvents.EnqueueEvent(ScheduledEvent.From(new SystemLog("msg"), 0UL));
        scheduler.SimulationStep();

        Assert.Single(sink1.Received);
        Assert.Single(sink2.Received);
        Assert.IsType<Event<SystemLog>>(sink1.Received[0]);
        Assert.IsType<Event<SystemLog>>(sink2.Received[0]);
    }
}