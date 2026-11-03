public sealed record MonitoringArchive(
    List<Reading> Readings,
    List<ImportError> Errors,
    List<Alert> Alerts,
    DateTimeOffset CreatedAtUtc);