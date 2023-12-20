using System.Diagnostics;

namespace termRTS.Engine;

/// <summary>
/// The one who ties the whole system together.
/// </summary>
internal class Core
{
    private static readonly TimeSpan MS_PER_UPDATE = TimeSpan.FromSeconds(1.0 / 60.0);

    private readonly Stopwatch _stopwatch;
    private bool _isGameRunning;

    public Core()
    {
        _stopwatch = new Stopwatch();
        _isGameRunning = false;
    }

    public void GameLoop()
    {
        _isGameRunning = true;
        _stopwatch.Start();
        var lag = TimeSpan.Zero;

        while (_isGameRunning)
        {
            _stopwatch.Stop();
            lag += _stopwatch.Elapsed;
            _stopwatch.Restart();

            // process input
            // input();

            // Advance as many steps as we need to minimize lag.
            while (lag >= MS_PER_UPDATE)
            {
                // run game logic
                // update();
                lag -= MS_PER_UPDATE;
            }

            var howFarIntoNextFrameMs = lag.TotalMilliseconds / MS_PER_UPDATE.TotalMilliseconds;
            var renderWatch = Stopwatch.StartNew();
            // render
            // render(howFarIntoNextFrameMs)
            renderWatch.Stop();
            var renderElapsed = renderWatch.Elapsed;
            Console.WriteLine($"render duration: {renderElapsed}");

            // take a break if we're ahead of time
            var loopTimeMs = lag + renderElapsed;

            // if we spent longer than
            if (loopTimeMs > MS_PER_UPDATE)
                continue;

            var sleepyTime = (int)(MS_PER_UPDATE - loopTimeMs).TotalMilliseconds;
            Console.WriteLine($"pausing game loop for {sleepyTime} ms");
            Thread.Sleep(sleepyTime);
        }
    }
}
