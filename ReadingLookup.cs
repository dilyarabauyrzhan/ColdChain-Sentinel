namespace ColdChainMonitor;

// Explicit lookup cases: a valid value such as 0 is not treated as absence.
public abstract record LookupResult<T>
{
    private LookupResult() { }
    public sealed record Found(T Value) : LookupResult<T>;
    public sealed record NotFound(string Key) : LookupResult<T>;
}

public static class ReadingLookup
{
    public static LookupResult<Reading> FindBySensor(
        IEnumerable<Reading> readings, string sensorId)
    {
        var match = readings.FirstOrDefault(r =>
            string.Equals(r.SensorId, sensorId, StringComparison.OrdinalIgnoreCase));
        return match is null
            ? new LookupResult<Reading>.NotFound(sensorId)
            : new LookupResult<Reading>.Found(match);
    }
}
