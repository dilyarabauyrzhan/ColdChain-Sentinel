namespace ColdChainMonitor;

public record NativeRiskReport(
    Reading Reading,
    double NativeRiskDistance,
    PipelineReport PipelineReport);

public static class ColdChainWorkflow
{
    // A required sensor lookup is converted to a domain Result failure.
    public static Result<PipelineReport, DomainError> CreateReportForSensor(
        IReadOnlyList<Reading> readings, string sensorId, Func<Reading, bool> policy)
    {
        Result<Reading, DomainError> found = ReadingLookup.FindBySensor(readings, sensorId) switch
        {
            LookupResult<Reading>.Found item => Result<Reading, DomainError>.Ok(item.Value),
            LookupResult<Reading>.NotFound missing => Result<Reading, DomainError>.Fail(
                new("SENSOR_NOT_FOUND", $"Required sensor '{missing.Key}' was not found.")),
            _ => Result<Reading, DomainError>.Fail(new("LOOKUP_ERROR", "Unknown lookup outcome."))
        };

        return found
            .Bind(ValidateReading)
            .Bind(reading => CheckOperationAllowed(reading)
                .Map(_ => ColdChainPipeline.Run(new[] { reading }, policy)));
    }


    public static Result<NativeRiskReport, DomainError>
        CreateNativeRiskReportForSensor(
            IReadOnlyList<Reading> readings,
            string sensorId,
            Func<Reading, bool> policy)
    {
        Result<Reading, DomainError> found =
            ReadingLookup.FindBySensor(readings, sensorId) switch
            {
                LookupResult<Reading>.Found item =>
                    Result<Reading, DomainError>.Ok(item.Value),

                LookupResult<Reading>.NotFound missing =>
                    Result<Reading, DomainError>.Fail(
                        new("SENSOR_NOT_FOUND",
                            $"Required sensor '{missing.Key}' was not found.")),

                _ => Result<Reading, DomainError>.Fail(
                    new("LOOKUP_ERROR", "Unknown lookup outcome."))
            };

        return found
            .Bind(ValidateReading)
            .Bind(reading => CheckOperationAllowed(reading)
                .Bind(validReading =>
                {
                    var range = ColdChainPipeline.GetSafeRange(validReading);

                    return NativeRisk.Calculate(
                        (double)validReading.Temperature,
                        (double)range.Min,
                        (double)range.Max)
                        .Map(nativeDistance =>
                            new NativeRiskReport(
                                validReading,
                                nativeDistance,
                                ColdChainPipeline.Run(
                                    new[] { validReading },
                                    policy)));
                }));
    }


    public static Result<Reading, DomainError> ValidateReading(Reading reading)
    {
        if (string.IsNullOrWhiteSpace(reading.SensorId))
            return Result<Reading, DomainError>.Fail(
                new("INVALID_SENSOR_ID", "Sensor ID is required."));
        if (!Enum.IsDefined(typeof(StorageClass), reading.StorageClass))
            return Result<Reading, DomainError>.Fail(
                new("INVALID_STORAGE_CLASS", $"Sensor '{reading.SensorId}' has an unsupported storage class."));
        if (reading.Temperature < -100m || reading.Temperature > 100m)
            return Result<Reading, DomainError>.Fail(
                new("INVALID_TEMPERATURE", $"Temperature for sensor '{reading.SensorId}' must be between -100 and 100 C."));
        return Result<Reading, DomainError>.Ok(reading);
    }

    // Demonstrates a distinct domain rule: this operation requires a non-default timestamp.
    public static Result<Reading, DomainError> CheckOperationAllowed(Reading reading)
    {
        if (reading.Timestamp == default)
            return Result<Reading, DomainError>.Fail(
                new("INVALID_TIMESTAMP", $"Sensor '{reading.SensorId}' has no valid timestamp."));
        return Result<Reading, DomainError>.Ok(reading);
    }
}
