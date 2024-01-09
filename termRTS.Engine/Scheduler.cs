using System.Diagnostics;

namespace termRTS.Engine;

public class Scheduler
{
    private readonly TimeSpan _msPerUpdate;
    private readonly Stopwatch _stopwatch;
    private readonly ICore _core;

    public Scheduler(double updateTimeSpan, ICore core)
    {
        _msPerUpdate = TimeSpan.FromSeconds(updateTimeSpan);
        _stopwatch = new Stopwatch();
        _core = core;
    }

    public void GameLoop()
    {
        _stopwatch.Start();
        var lag = TimeSpan.Zero;

        while (_core.IsGameRunning())
        {
            _stopwatch.Stop();
            lag += _stopwatch.Elapsed;
            _stopwatch.Restart();

            // STEP 1: INPUT
            _core.ProcessInput();

            // STEP 2: UPDATE
            while (lag >= _msPerUpdate)
            {
                _core.Tick();
                lag -= _msPerUpdate;
            }

            // STEP 3: RENDER
            var howFarIntoNextFrameMs = lag.TotalMilliseconds / _msPerUpdate.TotalMilliseconds;
            var renderWatch = Stopwatch.StartNew();
            _core.Render(howFarIntoNextFrameMs);
            renderWatch.Stop();
            Console.WriteLine($"render duration: {renderWatch.Elapsed}");
            var renderElapsed = renderWatch.Elapsed;

            // Take a break if we're ahead of time.
            var loopTimeMs = lag + renderElapsed;

            // If we spent longer than our allotted time, skip right ahead...
            if (loopTimeMs >= _msPerUpdate)
                continue;

            // ...otherwise wait until the next frame is due.
            var sleepyTime = (int)(_msPerUpdate - loopTimeMs).TotalMilliseconds;
            Console.WriteLine($"pausing game loop for {sleepyTime} ms");
            Thread.Sleep(sleepyTime);
        }
    }
}
