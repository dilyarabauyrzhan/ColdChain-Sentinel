public sealed record Alert(
    string SensorId,
    DateTimeOffset Timestamp,
    decimal Temperature,
    bool OutsideRange,
    bool AbruptChange);