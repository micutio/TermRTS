using TermRTS;

namespace TermRTS.Examples.Hillshade;

/// <summary>
///     Singleton component holding simulation time used for the day/night cycle (sun position).
///     Advanced each tick by <see cref="TimeOfDaySystem" />.
/// </summary>
public class TimeOfDayComponent : ComponentBase
{
    /// <summary>
    ///     Current time in milliseconds. Used to derive sun azimuth and altitude.
    ///     Wraps at <see cref="DayLengthMs" /> for a repeating day cycle.
    /// </summary>
    public ulong TimeMs { get; set; }

    /// <summary>
    ///     Length of one full day in milliseconds. Sun position is periodic over this interval.
    /// </summary>
    public ulong DayLengthMs { get; }

    public TimeOfDayComponent(int entityId, ulong initialTimeMs = 0, ulong dayLengthMs = 24 * 60 * 60 * 1000)
        : base(entityId)
    {
        TimeMs = initialTimeMs;
        DayLengthMs = dayLengthMs;
    }
}
