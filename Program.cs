using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColdChainMonitor
{
    internal class Program
    {
        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters =
        {
            new JsonStringEnumConverter()
        }
        };

        static void Main()
        {
            if (!File.Exists("readings.txt"))
            {
                Console.WriteLine("Data file 'readings.txt' was not found. Run from the ColdChainMonitor project/output directory.");
                return;
            }

            ImportResult importResult;
            using (FileStream inputStream = File.OpenRead("readings.txt"))
            {
                importResult = ColdChainProcessor.ReadReadings(inputStream);
            }

            // Assignment 6: validate readings into an explicit Valid/Invalid state
            // before the real alert-analysis workflow uses them.
            var validatedReadings = new List<Reading>();

            foreach (Reading reading in importResult.Readings)
            {
                ReadingValidationResult validation =
                    ValidatedReadingFactory.Create(reading);

                switch (validation)
                {
                    case ValidReading valid:
                        validatedReadings.Add(valid.Value.Value);
                        break;

                    case InvalidReading invalid:
                        Console.WriteLine(
                            $"Rejected reading {reading.SensorId}: " +
                            string.Join("; ", invalid.Reasons));
                        break;

                    default:
                        throw new InvalidOperationException(
                            "Unknown reading validation state.");
                }
            }

            List<Alert> alerts =
                ColdChainProcessor.AnalyzeReadings(validatedReadings);

            MonitoringArchive archive =
                new MonitoringArchive(
                    importResult.Readings,
                    importResult.Errors,
                    alerts,
                    DateTimeOffset.UtcNow);

            Console.WriteLine(
                $"Valid readings: {importResult.Readings.Count}");

            Console.WriteLine(
                $"Import errors: {importResult.Errors.Count}");

            Console.WriteLine(
                $"Alerts: {alerts.Count}");

            Console.WriteLine();
            Console.WriteLine("Import errors:");

            foreach (ImportError error in importResult.Errors)
            {
                Console.WriteLine(
                    $"Line {error.LineNumber}: {error.Message}");
            }

            Console.WriteLine();
            Console.WriteLine("Alerts:");

            foreach (Alert alert in alerts)
            {
                Console.WriteLine(
                    $"{alert.SensorId} | " +
                    $"{alert.Timestamp:yyyy-MM-dd HH:mm:ss} | " +
                    $"Temperature: {alert.Temperature} | " +
                    $"OutsideRange: {alert.OutsideRange} | " +
                    $"AbruptChange: {alert.AbruptChange}");
            }

            using var memoryStream = new MemoryStream();

            ColdChainProcessor.WriteArchive(
                memoryStream,
                archive);

            memoryStream.Position = 0;

            MonitoringArchive restoredArchive =
                ColdChainProcessor.ReadArchive(memoryStream);

            Console.WriteLine();

            Console.WriteLine(
                $"Restored readings: {restoredArchive.Readings.Count}");

            Console.WriteLine(
                $"Restored errors: {restoredArchive.Errors.Count}");

            Console.WriteLine(
                $"Restored alerts: {restoredArchive.Alerts.Count}");

            Console.WriteLine(
                $"Restored created at: {restoredArchive.CreatedAtUtc}");

            bool roundTripSuccessful =
                ColdChainProcessor.ArchivesAreEqual(
                    archive,
                    restoredArchive);

            Console.WriteLine(
                $"Round trip successful: {roundTripSuccessful}");

            using (FileStream archiveOutput =
                File.Create("archive.json"))
            {
                ColdChainProcessor.WriteArchive(
                    archiveOutput,
                    archive);
            }

            using FileStream archiveInput =
                File.OpenRead("archive.json");

            MonitoringArchive fileArchive =
                ColdChainProcessor.ReadArchive(
                    archiveInput);

            bool fileRoundTripSuccessful =
                ColdChainProcessor.ArchivesAreEqual(
                    archive,
                    fileArchive);

            Console.WriteLine(
                $"File round trip successful: {fileRoundTripSuccessful}");

        
            var standardReport = ColdChainPipeline.Run(
                importResult.Readings,
                RiskPolicies.Standard);

            var strictReport = ColdChainPipeline.Run(
                importResult.Readings,
                RiskPolicies.Strict);

            Console.WriteLine("STANDARD POLICY");
            Console.WriteLine(
                $"Input: {standardReport.InputCount}, " +
                $"selected: {standardReport.Items.Count}");

            foreach (var item in standardReport.Items)
            {
                Console.WriteLine(
                    $"{item.Reading.SensorId} | " +
                    $"{item.Reading.Temperature} | " +
                    $"{item.RiskLevel} | {item.RiskScore}");
            }

            Console.WriteLine("\nSTRICT POLICY");
            Console.WriteLine(
                $"Input: {strictReport.InputCount}, " +
                $"selected: {strictReport.Items.Count}");

            foreach (var item in strictReport.Items)
            {
                Console.WriteLine(
                    $"{item.Reading.SensorId} | " +
                    $"{item.Reading.Temperature} | " +
                    $"{item.RiskLevel} | {item.RiskScore}");
            }

            Console.WriteLine("\nASSIGNMENT 8: CURRYING");

            var createPolicy =
                CurriedRiskPolicy.CreateBoundaryPolicy();

            // Partial application: configure two independent policies.
            Func<Reading, bool> cautiousPolicy =
                createPolicy(0.5m);

            Func<Reading, bool> sensitivePolicy =
                createPolicy(1.5m);

            var cautiousReport = ColdChainPipeline.Run(
                validatedReadings,
                cautiousPolicy);

            var sensitiveReport = ColdChainPipeline.Run(
                validatedReadings,
                sensitivePolicy);

            Console.WriteLine(
                $"Cautious policy (0.5): {cautiousReport.Items.Count} selected");

            Console.WriteLine(
                $"Sensitive policy (1.5): {sensitiveReport.Items.Count} selected");

            Console.WriteLine("\nASSIGNMENT 8: DEFERRED EXECUTION");

            IEnumerable<Reading> deferredReadings =
                ReadingStream.FilterReadings(
                validatedReadings,
                sensitivePolicy);

            Console.WriteLine("Query created. No iteration yet.");

            // Materialization: the query executes here.
            Reading[] snapshot = deferredReadings.ToArray();

            Console.WriteLine("Query materialized into an array.");

            foreach (Reading reading in snapshot)
            {
                Console.WriteLine(
                    $"{reading.SensorId} | {reading.Temperature}");
            }

            Console.WriteLine("\nASSIGNMENT 8: RESOURCE MANAGEMENT");

            IEnumerable<Reading> fileReadings =
                ReadingFileStream.ReadValidReadings("readings.txt");

            using (var enumerator = fileReadings.GetEnumerator())
            {
                if (enumerator.MoveNext())
                {
                    Reading firstReading = enumerator.Current;

                    Console.WriteLine(
                        $"First reading: {firstReading.SensorId} | " +
                        $"{firstReading.Temperature}");

                    Console.WriteLine(
                        "Stopping enumeration early. File will be disposed.");
                }
            }

            Console.WriteLine("\nASSIGNMENT 8: NATIVE C LIBRARY");

            var nativeResult = NativeRisk.Calculate(
                temperature: 9.0,
                safeMin: 2.0,
                safeMax: 8.0);

            switch (nativeResult)
            {
                case Result<double, DomainError>.Success success:
                    Console.WriteLine(
                        $"Native risk calculation: {success.Value}");
                    break;

                case Result<double, DomainError>.Failure failure:
                    Console.WriteLine(
                        $"Native error [{failure.Error.Code}]: " +
                        failure.Error.Message);
                    break;
            }


            Console.WriteLine("\nNATIVE RISK WORKFLOW");

            var nativeWorkflowResult =
                ColdChainWorkflow.CreateNativeRiskReportForSensor(
                    validatedReadings,
                    "S1",
                    RiskPolicies.Standard);

            switch (nativeWorkflowResult)
            {
                case Result<NativeRiskReport, DomainError>.Success success:
                    Console.WriteLine(
                        $"Sensor: {success.Value.Reading.SensorId}");

                    Console.WriteLine(
                        $"Temperature: {success.Value.Reading.Temperature}");

                    Console.WriteLine(
                        $"Native risk distance: {success.Value.NativeRiskDistance}");

                    Console.WriteLine(
                        $"C# report selected: {success.Value.PipelineReport.Items.Count}");

                    break;

                case Result<NativeRiskReport, DomainError>.Failure failure:
                    Console.WriteLine(
                        $"Workflow failed [{failure.Error.Code}]: " +
                        failure.Error.Message);
                    break;
            }


            Console.WriteLine("\nCURRIED POLICY IN WORKFLOW");

            var configuredWorkflow =
                ColdChainWorkflow.CreateReportForSensor(
                    validatedReadings,
                    "S1",
                    sensitivePolicy);

            PrintWorkflowResult(configuredWorkflow);

            Console.WriteLine("\nASSIGNMENT 7 RESULT WORKFLOW");
            var workflowResult = ColdChainWorkflow.CreateReportForSensor(
                importResult.Readings, "S1", RiskPolicies.Standard);
            PrintWorkflowResult(workflowResult);

            var missingSensorResult = ColdChainWorkflow.CreateReportForSensor(
                importResult.Readings, "MISSING-SENSOR", RiskPolicies.Standard);
            PrintWorkflowResult(missingSensorResult);

            Console.WriteLine("\nASSIGNMENT 8: EXTENSION METHODS");

            // Custom extension method 1: filter readings by sensor.
            var sensorReadings = validatedReadings.ForSensor("S1");

            Console.WriteLine(
                $"Readings for sensor S1: {sensorReadings.Count()}");

            // Custom extension method 2: filter readings by time range.
            if (validatedReadings.Count > 0)
            {
                DateTimeOffset startTime =
                    validatedReadings.Min(r => r.Timestamp);

                DateTimeOffset endTime =
                    validatedReadings.Max(r => r.Timestamp);

                var periodReadings = validatedReadings.RecordedBetween(
                    startTime,
                    endTime);

                Console.WriteLine(
                    $"Readings in time range: {periodReadings.Count()}");
            }
            else
            {
                Console.WriteLine("No valid readings for time filtering.");
            }

            Console.WriteLine("\nASSIGNMENT 8: HIGH RISK REPORT");

            var highRiskReport =
                ReadingReports.CreateHighRiskReport(validatedReadings);

            foreach (var item in highRiskReport)
            {
                Console.WriteLine(
                    $"{item.SensorId} | " +
                    $"{item.Timestamp:yyyy-MM-dd HH:mm:ss} | " +
                    $"Temperature: {item.Temperature} | " +
                    $"Risk: {item.RiskLevel} | " +
                    $"Score: {item.RiskScore}");
            }

            Console.WriteLine(
                $"Total high-risk readings: {highRiskReport.Count}");

            Console.WriteLine("\nASSIGNMENT 8: SENSOR STATISTICS REPORT");

            var statisticsReport =
                ReadingReports.CreateSensorStatisticsReport(validatedReadings);

            foreach (var item in statisticsReport)
            {
                Console.WriteLine(
                    $"{item.SensorId} | " +
                    $"Count: {item.ReadingCount} | " +
                    $"Average: {item.AverageTemperature:F2} | " +
                    $"Maximum: {item.MaximumTemperature} | " +
                    $"Total: {item.TotalTemperature}");
            }

            Console.WriteLine("\nFILE FAILURE DEMONSTRATIONS");
            PrintFileResult(DataFileReader.ReadJson("missing-sample.json"));
            string malformedPath = Path.Combine(Path.GetTempPath(), "cold-chain-malformed.json");
            try
            {
                File.WriteAllText(malformedPath, "{ this is not valid JSON");
                PrintFileResult(DataFileReader.ReadJson(malformedPath));
            }
            finally
            {
                if (File.Exists(malformedPath)) File.Delete(malformedPath);
            }
        }

        private static void PrintWorkflowResult(Result<PipelineReport, DomainError> result)
        {
            switch (result)
            {
                case Result<PipelineReport, DomainError>.Success success:
                    Console.WriteLine($"Workflow succeeded. Input: {success.Value.InputCount}, selected: {success.Value.Items.Count}");
                    break;
                case Result<PipelineReport, DomainError>.Failure failure:
                    Console.WriteLine($"Workflow failed [{failure.Error.Code}]: {failure.Error.Message}");
                    break;
            }
        }

        private static void PrintFileResult(Result<IReadOnlyList<Reading>, DomainError> result)
        {
            switch (result)
            {
                case Result<IReadOnlyList<Reading>, DomainError>.Success success:
                    Console.WriteLine($"File loaded: {success.Value.Count} readings.");
                    break;
                case Result<IReadOnlyList<Reading>, DomainError>.Failure failure:
                    Console.WriteLine($"File error [{failure.Error.Code}]: {failure.Error.Message}");
                    break;
            }
        }
    }
}