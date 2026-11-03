public sealed record ImportResult(
    List<Reading> Readings,
    List<ImportError> Errors);