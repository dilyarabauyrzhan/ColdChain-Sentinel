
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColdChainMonitor
{
    // One row in the high-risk report.
    public record HighRiskReportRow(
        string SensorId,
        DateTimeOffset Timestamp,
        decimal Temperature,
        string RiskLevel,
        decimal RiskScore);

    // One row in the sensor statistics report.
    public record SensorStatisticsRow(
        string SensorId,
        int ReadingCount,
        decimal AverageTemperature,
        decimal MaximumTemperature,
        decimal TotalTemperature);

    public static class ReadingReports
    {
        // Report 1: readings with high risk.
        public static IReadOnlyList<HighRiskReportRow>
            CreateHighRiskReport(IEnumerable<Reading> readings)
        {
            return ColdChainPipeline.CalculateRisk(readings)
                .Where(item => item.RiskLevel == "High")
                .Select(item => new HighRiskReportRow(
                    item.Reading.SensorId,
                    item.Reading.Timestamp,
                    item.Reading.Temperature,
                    item.RiskLevel,
                    item.RiskScore))
                .OrderByDescending(item => item.RiskScore)
                .ThenBy(item => item.SensorId,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Timestamp)
                .ToArray();
        }

        // Report 2: statistics grouped by sensor.
        public static IReadOnlyList<SensorStatisticsRow>
            CreateSensorStatisticsReport(IEnumerable<Reading> readings)
        {
            return readings
                .GroupBy(item => item.SensorId,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => new SensorStatisticsRow(
                    group.Key,
                    group.Count(),
                    group.Average(item => item.Temperature),
                    group.Max(item => item.Temperature),
                    group.Sum(item => item.Temperature)))
                .OrderByDescending(item => item.ReadingCount)
                .ThenBy(item => item.SensorId,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
