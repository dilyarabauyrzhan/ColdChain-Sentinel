using System;
using System.Collections.Generic;
using System.Linq;

namespace ColdChainMonitor
{
    public record RiskItem(
        Reading Reading,
        decimal RiskScore,
        string RiskLevel);

    public record PipelineReport(
        int InputCount,
        IReadOnlyList<RiskItem> Items);

    public static class ColdChainPipeline
    {
        // Stage 1: select readings using the chosen policy
        public static IReadOnlyList<Reading> SelectReadings(
            IEnumerable<Reading> readings,
            Func<Reading, bool> policy)
        {
            return Array.AsReadOnly(
                readings.Where(policy).ToArray());
        }

        // Stage 2: calculate a risk score
        public static IReadOnlyList<RiskItem> CalculateRisk(
            IEnumerable<Reading> readings)
        {
            return Array.AsReadOnly(
                readings.Select(r =>
                {
                    decimal min = r.StorageClass == StorageClass.Cold
                        ? 2.0m : -22.0m;

                    decimal max = r.StorageClass == StorageClass.Cold
                        ? 8.0m : -15.0m;

                    decimal distance = r.Temperature < min
                        ? min - r.Temperature
                        : r.Temperature > max
                            ? r.Temperature - max
                            : Math.Min(
                                r.Temperature - min,
                                max - r.Temperature);

                    bool outside =
                        r.Temperature < min || r.Temperature > max;

                    decimal score = outside
                        ? 10m + distance
                        : distance <= 0.5m ? 5m : 0m;

                    string level = score >= 10m
                        ? "High"
                        : score >= 5m ? "Medium" : "Low";

                    return new RiskItem(r, score, level);
                }).ToArray());
        }

        // Stage 3: create an ordered report
        public static PipelineReport CreateReport(
            int inputCount,
            IEnumerable<RiskItem> items)
        {
            var ordered = items
                .OrderByDescending(x => x.RiskScore)
                .ThenBy(x => x.Reading.SensorId,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new PipelineReport(
                inputCount,
                Array.AsReadOnly(ordered));
        }

        // Compose the three stages
        public static PipelineReport Run(
            IReadOnlyList<Reading> readings,
            Func<Reading, bool> policy)
        {
            var selected = SelectReadings(readings, policy);
            var scored = CalculateRisk(selected);

            return CreateReport(readings.Count, scored);
        }

        public static bool IsOutsideSafeRange(Reading r)
        {
            return r.StorageClass == StorageClass.Cold
                ? r.Temperature < 2.0m || r.Temperature > 8.0m
                : r.Temperature < -22.0m || r.Temperature > -15.0m;
        }


        public static bool IsNearBoundary(Reading r)
        {
            return IsNearBoundary(r, 0.5m);
        }

        public static bool IsNearBoundary(Reading r, decimal margin)
        {
            ArgumentNullException.ThrowIfNull(r);

            if (margin < 0)
                throw new ArgumentOutOfRangeException(nameof(margin));

            decimal min = r.StorageClass == StorageClass.Cold
                ? 2.0m : -22.0m;

            decimal max = r.StorageClass == StorageClass.Cold
                ? 8.0m : -15.0m;

            return !IsOutsideSafeRange(r)
                && Math.Min(
                    r.Temperature - min,
                    max - r.Temperature) <= margin;
        }

        public static (decimal Min, decimal Max) GetSafeRange(
            Reading reading)
        {
            ArgumentNullException.ThrowIfNull(reading);

            return reading.StorageClass == StorageClass.Cold
                ? (2.0m, 8.0m)
                : (-22.0m, -15.0m);
        }

    }
}
