
namespace ColdChainMonitor;

public static class ReadingExtensions
{
    // Returns readings belonging to a specific sensor.
    public static IEnumerable<Reading> ForSensor(
        this IEnumerable<Reading> readings,
        string sensorId)
    {
        ArgumentNullException.ThrowIfNull(readings);
        ArgumentException.ThrowIfNullOrWhiteSpace(sensorId);

        return readings.Where(r =>
            string.Equals(
                r.SensorId,
                sensorId,
                StringComparison.OrdinalIgnoreCase));
    }

    // Returns readings recorded within a specified time period.
    public static IEnumerable<Reading> RecordedBetween(
        this IEnumerable<Reading> readings,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        ArgumentNullException.ThrowIfNull(readings);

        if (start > end)
            throw new ArgumentException("Start date cannot be later than end date.");

        return readings.Where(r =>
            r.Timestamp >= start && r.Timestamp <= end);
    }
}
