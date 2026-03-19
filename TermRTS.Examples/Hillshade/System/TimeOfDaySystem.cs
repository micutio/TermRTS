using TermRTS.Storage;

namespace TermRTS.Examples.Hillshade.System;

public class TimeOfDaySystem : ISimSystem
{
    #region ISimSystem Members

    public void ProcessComponents(ulong timeStepSizeMs, in IReadonlyStorage storage)
    {
        if (!storage.TryGetSingleForType<TimeOfDayComponent>(out var timeOfDay) || timeOfDay == null)
            return;

        timeOfDay.TimeMs += timeStepSizeMs;
        if (timeOfDay.DayLengthMs > 0 && timeOfDay.TimeMs >= timeOfDay.DayLengthMs)
            timeOfDay.TimeMs %= timeOfDay.DayLengthMs;
    }

    #endregion
}
