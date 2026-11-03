
using System;
using System.Collections.Generic;

namespace ColdChainMonitor;

public static class ReadingStream
{
    public static IEnumerable<Reading> FilterReadings(
        IEnumerable<Reading> readings,
        Func<Reading, bool> policy)
    {
        ArgumentNullException.ThrowIfNull(readings);
        ArgumentNullException.ThrowIfNull(policy);

        foreach (Reading reading in readings)
        {
            if (policy(reading))
            {
                yield return reading;
            }
        }
    }
}
