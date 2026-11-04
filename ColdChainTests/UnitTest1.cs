using System;
using System.Collections.Generic;
using System.Linq;
using ColdChainMonitor;
using Xunit;

namespace ColdChainTests
{
    public class PipelineTests
    {
        // Test 1: Standard policy selects only readings outside the safe range.
        [Fact]
        public void StandardPolicy_SelectsOnlyOutOfRangeReadings()
        {
            var readings = new List<Reading>
            {
                new Reading("S1", DateTimeOffset.Now, StorageClass.Cold, 9.0m),
                new Reading("S2", DateTimeOffset.Now, StorageClass.Cold, 5.0m)
            };

            var report = ColdChainPipeline.Run(
                readings, RiskPolicies.Standard);

            Assert.Single(report.Items);
            Assert.Equal("S1", report.Items[0].Reading.SensorId);
            Assert.Equal(9.0m, report.Items[0].Reading.Temperature);
        }

        // Test 2: Strict policy also selects readings near a safe-range boundary.
        [Fact]
        public void StrictPolicy_SelectsNearBoundaryReadings()
        {
            var readings = new List<Reading>
            {
                new Reading("S1", DateTimeOffset.Now, StorageClass.Cold, 7.8m),
                new Reading("S2", DateTimeOffset.Now, StorageClass.Cold, 5.0m)
            };

            var standardReport = ColdChainPipeline.Run(
                readings, RiskPolicies.Standard);

            var strictReport = ColdChainPipeline.Run(
                readings, RiskPolicies.Strict);

            Assert.Empty(standardReport.Items);
            Assert.Single(strictReport.Items);
            Assert.Equal("S1", strictReport.Items[0].Reading.SensorId);
        }

        // Test 3: Report items are sorted by descending risk score.
        [Fact]
        public void Report_SortsItemsByRiskScoreDescending()
        {
            var readings = new List<Reading>
            {
                new Reading("S1", DateTimeOffset.Now, StorageClass.Cold, 9.0m),
                new Reading("S2", DateTimeOffset.Now, StorageClass.Frozen, -12.0m),
                new Reading("S3", DateTimeOffset.Now, StorageClass.Cold, 8.7m)
            };

            var report = ColdChainPipeline.Run(
                readings, RiskPolicies.Standard);

            Assert.Equal(3, report.Items.Count);
            Assert.Equal("S2", report.Items[0].Reading.SensorId);
            Assert.Equal("S1", report.Items[1].Reading.SensorId);
            Assert.Equal("S3", report.Items[2].Reading.SensorId);

            Assert.True(report.Items[0].RiskScore >= report.Items[1].RiskScore);
            Assert.True(report.Items[1].RiskScore >= report.Items[2].RiskScore);
        }

        // Test 4: An empty dataset produces an empty report.
        [Fact]
        public void EmptyDataset_ReturnsEmptyReport()
        {
            var readings = new List<Reading>();

            var report = ColdChainPipeline.Run(
                readings, RiskPolicies.Strict);

            Assert.Equal(0, report.InputCount);
            Assert.Empty(report.Items);
        }

        // Test 5: The pipeline does not modify the original input list.
        [Fact]
        public void Pipeline_DoesNotModifyOriginalReadings()
        {
            var readings = new List<Reading>
            {
                new Reading("S1", DateTimeOffset.Now, StorageClass.Cold, 9.0m),
                new Reading("S2", DateTimeOffset.Now, StorageClass.Cold, 5.0m)
            };

            var originalReadings = readings.ToArray();

            ColdChainPipeline.Run(readings, RiskPolicies.Strict);

            Assert.Equal(originalReadings, readings);
        }

        // Test 6: An out-of-range reading receives a High risk level.
        [Fact]
        public void CalculateRisk_OutOfRangeReadingHasHighRisk()
        {
            var readings = new List<Reading>
            {
                new Reading("S1", DateTimeOffset.Now, StorageClass.Cold, 9.0m)
            };

            var results = ColdChainPipeline.CalculateRisk(readings);

            Assert.Single(results);
            Assert.Equal(11.0m, results[0].RiskScore);
            Assert.Equal("High", results[0].RiskLevel);
        }

        // Test 7: Empty input is rejected.
        [Fact]
        public void EmptyInput_IsRejected()
        {
            bool result = ColdChainProcessor.TryParseReading(
                "", out _, out _);

            Assert.False(result);
        }
    }

    //Assignment 6 tests
    public class Assignment6ValidationTests
    {
        private static Reading MakeReading(
            string sensorId = "S1",
            decimal temperature = 5m,
            StorageClass storageClass = StorageClass.Cold)
            => new(sensorId, DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                storageClass, temperature);

        [Fact]
        public void Factory_ValidInputCreatesValidatedValue()
        {
            var result = ValidatedReadingFactory.Create(MakeReading());

            var valid = Assert.IsType<ValidReading>(result);
            Assert.Equal("S1", valid.Value.Value.SensorId);
            Assert.Equal(5m, valid.Value.Value.Temperature);
        }

        [Fact]
        public void Factory_RejectsEmptySensorIdWithReason()
        {
            var result = ValidatedReadingFactory.Create(MakeReading(sensorId: " "));

            var invalid = Assert.IsType<InvalidReading>(result);
            Assert.Contains(invalid.Reasons, reason => reason.Contains("Sensor ID"));
        }

        [Fact]
        public void Factory_RejectsPhysicallyImplausibleTemperature()
        {
            var result = ValidatedReadingFactory.Create(MakeReading(temperature: 101m));

            var invalid = Assert.IsType<InvalidReading>(result);
            Assert.Contains(invalid.Reasons, reason => reason.Contains("-100 and 100"));
        }

        [Fact]
        public void Factory_RejectsMissingReading()
        {
            var result = ValidatedReadingFactory.Create(null);

            var invalid = Assert.IsType<InvalidReading>(result);
            Assert.Contains("Reading is required.", invalid.Reasons);
        }

        [Fact]
        public void Factory_AcceptsExactPhysicalBoundary()
        {
            var result = ValidatedReadingFactory.Create(MakeReading(temperature: 100m));

            Assert.IsType<ValidReading>(result);
        }

        [Fact]
        public void PatternMatching_ValidCaseRunsWorkflow()
        {
            ReadingValidationResult validation =
                ValidatedReadingFactory.Create(MakeReading(temperature: 9m));

            var outcome = SafeReadingWorkflow.Analyze(validation);

            var succeeded = Assert.IsType<ReadingWorkflowSucceeded>(outcome);
            Assert.Single(succeeded.Alerts);
            Assert.True(succeeded.Alerts[0].OutsideRange);
        }

        [Fact]
        public void PatternMatching_InvalidCaseRejectsMissingPrerequisite()
        {
            ReadingValidationResult validation =
                ValidatedReadingFactory.Create(MakeReading(temperature: 101m));

            var outcome = SafeReadingWorkflow.Analyze(validation);

            var rejected = Assert.IsType<ReadingWorkflowRejected>(outcome);
            Assert.NotEmpty(rejected.Reasons);
        }

        [Fact]
        public void RepeatedOperation_IsSafeAndReturnsSameOutcome()
        {
            ReadingValidationResult validation =
                ValidatedReadingFactory.Create(MakeReading(temperature: 9m));

            var first = SafeReadingWorkflow.Analyze(validation);
            var second = SafeReadingWorkflow.Analyze(validation);

            var firstSuccess = Assert.IsType<ReadingWorkflowSucceeded>(first);
            var secondSuccess = Assert.IsType<ReadingWorkflowSucceeded>(second);
            Assert.Equal(firstSuccess.Alerts, secondSuccess.Alerts);
        }

        [Fact]
        public void FailedValidation_DoesNotModifyOriginalReading()
        {
            var original = MakeReading(sensorId: "", temperature: 101m);
            var snapshot = original with { };

            var result = ValidatedReadingFactory.Create(original);

            Assert.IsType<InvalidReading>(result);
            Assert.Equal(snapshot, original);
        }
    }


//Assignment 7 tests
public class Assignment7ResultTests
{
    private static Reading MakeReading(string id = "S1", decimal temperature = 5m, DateTimeOffset? timestamp = null) =>
        new(id, timestamp ?? DateTimeOffset.Parse("2026-01-01T00:00:00Z"), StorageClass.Cold, temperature);

    [Fact]
    public void ResultMap_TransformsSuccessValue()
    {
        var result = Result<int, DomainError>.Ok(4).Map(x => x * 2);
        Assert.Equal(8, Assert.IsType<Result<int, DomainError>.Success>(result).Value);
    }

    [Fact]
    public void ResultBind_ContinuesWithNextResult()
    {
        var result = Result<int, DomainError>.Ok(4)
            .Bind(x => Result<string, DomainError>.Ok($"value-{x}"));
        Assert.Equal("value-4", Assert.IsType<Result<string, DomainError>.Success>(result).Value);
    }

    [Fact]
    public void MapAndBind_DoNotInvokeFunctionsAfterFailure()
    {
        int calls = 0;
        var failed = Result<int, DomainError>.Fail(new("TEST", "expected failure"));
        var mapped = failed.Map(x => { calls++; return x + 1; });
        var bound = failed.Bind(x => { calls++; return Result<int, DomainError>.Ok(x + 1); });
        Assert.Equal(0, calls);
        Assert.IsType<Result<int, DomainError>.Failure>(mapped);
        Assert.IsType<Result<int, DomainError>.Failure>(bound);
    }

    [Fact]
    public void Lookup_ReturnsFoundForLegitimateZeroTemperature()
    {
        var result = ReadingLookup.FindBySensor(new[] { MakeReading(temperature: 0m) }, "S1");
        var found = Assert.IsType<LookupResult<Reading>.Found>(result);
        Assert.Equal(0m, found.Value.Temperature);
    }

    [Fact]
    public void Lookup_ReturnsNotFoundForMissingSensor()
    {
        var result = ReadingLookup.FindBySensor(new[] { MakeReading() }, "S2");
        Assert.Equal("S2", Assert.IsType<LookupResult<Reading>.NotFound>(result).Key);
    }

    [Fact]
    public void Workflow_ReturnsUsefulMissingSensorFailure()
    {
        var result = ColdChainWorkflow.CreateReportForSensor(new[] { MakeReading() }, "S2", RiskPolicies.Standard);
        var failure = Assert.IsType<Result<PipelineReport, DomainError>.Failure>(result);
        Assert.Equal("SENSOR_NOT_FOUND", failure.Error.Code);
    }

    [Fact]
    public void Workflow_RejectsInvalidTemperature()
    {
        var result = ColdChainWorkflow.CreateReportForSensor(new[] { MakeReading(temperature: 101m) }, "S1", RiskPolicies.Standard);
        var failure = Assert.IsType<Result<PipelineReport, DomainError>.Failure>(result);
        Assert.Equal("INVALID_TEMPERATURE", failure.Error.Code);
    }

    [Fact]
    public void Workflow_RejectsDefaultTimestamp()
    {
        var invalidTimestampReading = new Reading("S1", default, StorageClass.Cold, 5m);
        var result = ColdChainWorkflow.CreateReportForSensor(new[] { invalidTimestampReading }, "S1", RiskPolicies.Standard);
        var failure = Assert.IsType<Result<PipelineReport, DomainError>.Failure>(result);
        Assert.Equal("INVALID_TIMESTAMP", failure.Error.Code);
    }

    [Fact]
    public void DataFileReader_ReturnsMissingFileFailure()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var result = DataFileReader.ReadJson(path);
        var failure = Assert.IsType<Result<IReadOnlyList<Reading>, DomainError>.Failure>(result);
        Assert.Equal("FILE_NOT_FOUND", failure.Error.Code);
    }

    [Fact]
    public void DataFileReader_ReturnsMalformedJsonFailureWithoutPartialReadings()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, "{ not json }");
            var result = DataFileReader.ReadJson(path);
            var failure = Assert.IsType<Result<IReadOnlyList<Reading>, DomainError>.Failure>(result);
            Assert.Equal("MALFORMED_JSON", failure.Error.Code);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Workflow_CompleteSuccessReturnsReport()
    {
        var result = ColdChainWorkflow.CreateReportForSensor(new[] { MakeReading(temperature: 9m) }, "S1", RiskPolicies.Standard);
        var success = Assert.IsType<Result<PipelineReport, DomainError>.Success>(result);
        Assert.Single(success.Value.Items);
        Assert.Equal("S1", success.Value.Items[0].Reading.SensorId);
    }
}

}
