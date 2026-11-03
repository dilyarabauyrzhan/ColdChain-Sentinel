using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColdChainMonitor
{
    public static class ColdChainProcessor
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

        public static bool TryParseReading(
            string line,
            out Reading? reading,
            out string? errorMessage)
        {
            reading = null;
            errorMessage = null;

            string[] parts = line.Split('|');

            if (parts.Length != 4)
            {
                errorMessage = "Line must contain exactly four fields.";
                return false;
            }

            string sensorId = parts[0].Trim();
            string timestampText = parts[1].Trim();
            string storageClassText = parts[2].Trim();
            string temperatureText = parts[3].Trim();

            if (string.IsNullOrWhiteSpace(sensorId))
            {
                errorMessage = "Sensor ID cannot be empty.";
                return false;
            }

            if (!DateTimeOffset.TryParseExact(
                    timestampText,
                    "yyyy-MM-dd'T'HH:mm:ss'Z'",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal |
                    DateTimeStyles.AdjustToUniversal,
                    out DateTimeOffset timestamp))
            {
                errorMessage = "Invalid timestamp.";
                return false;
            }

            if (!Enum.TryParse<StorageClass>(
                    storageClassText,
                    true,
                    out StorageClass storageClass)
                || !Enum.IsDefined(
                    typeof(StorageClass),
                    storageClass))
            {
                errorMessage = "Invalid storage class.";
                return false;
            }

            const NumberStyles TemperatureStyle =
                NumberStyles.AllowLeadingSign |
                NumberStyles.AllowDecimalPoint;

            if (!decimal.TryParse(
                    temperatureText,
                    TemperatureStyle,
                    CultureInfo.InvariantCulture,
                    out decimal temperature))
            {
                errorMessage = "Invalid temperature.";
                return false;
            }

            reading = new Reading(
                sensorId,
                timestamp,
                storageClass,
                temperature);

            return true;
        }

        public static ImportResult ReadReadings(Stream input)
        {
            var readings = new List<Reading>();
            var errors = new List<ImportError>();

            var sensorStorageClasses =
                new Dictionary<string, StorageClass>(
                    StringComparer.OrdinalIgnoreCase);

            using var reader = new StreamReader(
                input,
                leaveOpen: true);

            string? line;
            int lineNumber = 0;

            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (TryParseReading(
                        line,
                        out Reading? reading,
                        out string? errorMessage))
                {
                    if (sensorStorageClasses.TryGetValue(
                            reading!.SensorId,
                            out StorageClass previousStorageClass))
                    {
                        if (previousStorageClass !=
                            reading.StorageClass)
                        {
                            errors.Add(new ImportError(
                                lineNumber,
                                line,
                                "Storage class changed for the same sensor."));
                            continue;
                        }
                    }
                    else
                    {
                        sensorStorageClasses[reading.SensorId] =
                            reading.StorageClass;
                    }

                    readings.Add(reading);
                }
                else
                {
                    errors.Add(new ImportError(
                        lineNumber,
                        line,
                        errorMessage!));
                }
            }

            return new ImportResult(readings, errors);
        }

        public static List<Alert> AnalyzeReadings(
            List<Reading> readings)
        {
            var alerts = new List<Alert>();

            var groupedReadings = readings
                .GroupBy(
                    reading => reading.SensorId,
                    StringComparer.OrdinalIgnoreCase);

            foreach (var sensorGroup in groupedReadings)
            {
                var orderedReadings = sensorGroup
                    .OrderBy(reading => reading.Timestamp)
                    .ToList();

                for (int i = 0;
                     i < orderedReadings.Count;
                     i++)
                {
                    Reading current = orderedReadings[i];

                    bool outsideRange =
                        IsOutsideSafeRange(current);

                    bool abruptChange = false;

                    if (i > 0)
                    {
                        Reading previous =
                            orderedReadings[i - 1];

                        abruptChange =
                            Math.Abs(
                                current.Temperature -
                                previous.Temperature) > 4.0m;
                    }

                    if (outsideRange || abruptChange)
                    {
                        alerts.Add(new Alert(
                            current.SensorId,
                            current.Timestamp,
                            current.Temperature,
                            outsideRange,
                            abruptChange));
                    }
                }
            }

            return alerts
                .OrderBy(
                    alert => alert.SensorId,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(alert => alert.Timestamp)
                .ToList();
        }

        private static bool IsOutsideSafeRange(
            Reading reading)
        {
            decimal minTemperature;
            decimal maxTemperature;

            if (reading.StorageClass == StorageClass.Cold)
            {
                minTemperature = 2.0m;
                maxTemperature = 8.0m;
            }
            else
            {
                minTemperature = -22.0m;
                maxTemperature = -15.0m;
            }

            return reading.Temperature < minTemperature
                || reading.Temperature > maxTemperature;
        }

        public static void WriteArchive(
            Stream output,
            MonitoringArchive archive)
        {
            JsonSerializer.Serialize(
                output,
                archive,
                JsonOptions);
        }

        public static MonitoringArchive ReadArchive(
            Stream input)
        {
            MonitoringArchive? archive =
                JsonSerializer.Deserialize<MonitoringArchive>(
                    input,
                    JsonOptions);

            if (archive is null)
            {
                throw new InvalidOperationException(
                    "Deserialized archive cannot be null.");
            }

            return archive;
        }

        public static Reading GetReadingAtPosition(IEnumerable<Reading> readings, int A)
        {
            var sorted = readings
                .OrderBy(r => r.SensorId)
                .ThenBy(r => r.Timestamp)
                .ThenBy(r => r.Temperature);
            return sorted.ElementAt(A);
        }

        public static bool ArchivesAreEqual(
            MonitoringArchive first,
            MonitoringArchive second)
        {
            return first.Readings.SequenceEqual(second.Readings)
                && first.Errors.SequenceEqual(second.Errors)
                && first.Alerts.SequenceEqual(second.Alerts)
                && first.CreatedAtUtc == second.CreatedAtUtc;
        }
    }
}