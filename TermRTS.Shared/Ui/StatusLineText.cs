namespace TermRTS.Shared.Ui;

/// <summary>
///     Helpers for status / debug lines (elapsed time + optional profile text).
/// </summary>
public static class StatusLineText
{
    /// <summary>
    ///     Formats <c>HH:mm:ss</c> elapsed wall time plus optional profile suffix (e.g. <c>| ms/frame ...</c>).
    /// </summary>
    public static string FormatElapsedClock(double timePassedMs, string? profileOutput)
    {
        var debugStr = string.IsNullOrEmpty(profileOutput)
            ? string.Empty
            : profileOutput;
        var sec = (int)Math.Floor(timePassedMs / 1000) % 60;
        var min = (int)Math.Floor(timePassedMs / (1000 * 60)) % 60;
        var hr = (int)Math.Floor(timePassedMs / (1000 * 60 * 60)) % 24;
        return $"{hr:D2}:{min:D2}:{sec:D2} | {debugStr}";
    }

    /// <summary>
    ///     One-line status: <c>{appName} | T HH:mm:ss</c> and optional <c> | {profile}</c>.
    /// </summary>
    public static string FormatWithAppLabel(string appName, double timePassedMs, string? profileOutput)
    {
        var sec = (int)Math.Floor(timePassedMs / 1000) % 60;
        var min = (int)Math.Floor(timePassedMs / (1000 * 60)) % 60;
        var hr = (int)Math.Floor(timePassedMs / (1000 * 60 * 60)) % 24;
        var line = $"{appName} | T {hr:D2}:{min:D2}:{sec:D2}";
        if (!string.IsNullOrEmpty(profileOutput))
            line += $" | {profileOutput}";
        return line;
    }
}
