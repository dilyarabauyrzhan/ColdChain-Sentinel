using System;
using System.Collections.Generic;
using System.Linq;

namespace ColdChainMonitor
{
    // Closed result model: only a validated value or explicit rejection is returned.
    public abstract record ReadingValidationResult;

    public sealed record ValidReading(ValidatedReading Value)
        : ReadingValidationResult;

    public sealed record InvalidReading(IReadOnlyList<string> Reasons)
        : ReadingValidationResult;

    // The constructor is private, so callers cannot create this type without validation.
    public sealed class ValidatedReading
    {
        public Reading Value { get; }

        private ValidatedReading(Reading value)
        {
            Value = value;
        }

        internal static ValidatedReading FromFactory(Reading value)
            => new(value);
    }

    public static class ValidatedReadingFactory
    {
        public static ReadingValidationResult Create(Reading? candidate)
        {
            var errors = new List<string>();

            if (candidate is null)
            {
                errors.Add("Reading is required.");
                return new InvalidReading(Array.AsReadOnly(errors.ToArray()));
            }

            if (string.IsNullOrWhiteSpace(candidate.SensorId))
                errors.Add("Sensor ID cannot be empty.");

            if (!Enum.IsDefined(typeof(StorageClass), candidate.StorageClass))
                errors.Add("Storage class is not supported.");

            // Broad physical plausibility check, not the safe-storage range.
            // Out-of-range temperatures must remain valid readings so they can trigger alerts.
            if (candidate.Temperature < -100m || candidate.Temperature > 100m)
                errors.Add("Temperature must be between -100 and 100 degrees Celsius.");

            if (errors.Count > 0)
                return new InvalidReading(Array.AsReadOnly(errors.ToArray()));

            return new ValidReading(ValidatedReading.FromFactory(candidate));
        }
    }

    public abstract record ReadingWorkflowOutcome;

    public sealed record ReadingWorkflowSucceeded(IReadOnlyList<Alert> Alerts)
        : ReadingWorkflowOutcome;

    public sealed record ReadingWorkflowRejected(IReadOnlyList<string> Reasons)
        : ReadingWorkflowOutcome;

    public static class SafeReadingWorkflow
    {
        // A repeated call is safe: the operation is pure and returns the same outcome
        // for the same validation result. It does not mutate the validated value.
        public static ReadingWorkflowOutcome Analyze(ReadingValidationResult validation)
        {
            return validation switch
            {
                ValidReading valid => new ReadingWorkflowSucceeded(
                    Array.AsReadOnly(
                        ColdChainProcessor.AnalyzeReadings(
                            new List<Reading> { valid.Value.Value }).ToArray())),

                InvalidReading invalid => new ReadingWorkflowRejected(invalid.Reasons),

                _ => new ReadingWorkflowRejected(
                    Array.AsReadOnly(new[] { "Unexpected validation result." }))
            };
        }
    }
}
