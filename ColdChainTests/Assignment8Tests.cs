
using System;
using System.Collections.Generic;
using System.Linq;
using ColdChainMonitor;
using Xunit;

namespace ColdChainTests
{
    public class Assignment8Tests
    {
        private static readonly DateTimeOffset Time1 =
            DateTimeOffset.Parse("2026-01-01T10:00:00Z");

        private static readonly DateTimeOffset Time2 =
            DateTimeOffset.Parse("2026-01-01T11:00:00Z");

        private static Reading MakeReading(
            string sensorId = "S1",
            decimal temperature = 5m,
            DateTimeOffset? timestamp = null)
        {
            return new Reading(
                sensorId,
                timestamp ?? Time1,
                StorageClass.Cold,
                temperature);
        }

        // Extension method tests

        [Fact]
        public void ForSensor_FiltersReadingsCaseInsensitively()
        {
            var readings = new[]
            {
                MakeReading("S1"),
                MakeReading("S2"),
                MakeReading("s1")
            };

            var result = readings.ForSensor("S1").ToList();

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void ForSensor_ReturnsEmptyWhenSensorDoesNotExist()
        {
            var readings = new[]
            {
                MakeReading("S1")
            };

            var result = readings.ForSensor("S3");

            Assert.Empty(result);
        }

        [Fact]
        public void ForSensor_RejectsEmptySensorId()
        {
            var readings = new[] { MakeReading() };

            Assert.Throws<ArgumentException>(
                () => readings.ForSensor(" "));
        }

        [Fact]
        public void RecordedBetween_IncludesBoundaryReadings()
        {
            var readings = new[]
            {
                MakeReading(timestamp: Time1),
                MakeReading(timestamp: Time2),
                MakeReading(timestamp: Time2.AddHours(1))
            };

            var result = readings.RecordedBetween(
                Time1, Time2).ToList();

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void RecordedBetween_RejectsReversedDateRange()
        {
            var readings = new[] { MakeReading() };

            Assert.Throws<ArgumentException>(
                () => readings.RecordedBetween(Time2, Time1));
        }

        // LINQ report tests

        [Fact]
        public void HighRiskReport_ReturnsEmptyForEmptyInput()
        {
            var result =
                ReadingReports.CreateHighRiskReport(
                    Array.Empty<Reading>());

            Assert.Empty(result);
        }

        [Fact]
        public void HighRiskReport_ReturnsEmptyWhenNoHighRiskReadingsExist()
        {
            var readings = new[]
            {
                MakeReading("S1", 5m),
                MakeReading("S2", 6m)
            };

            var result =
                ReadingReports.CreateHighRiskReport(readings);

            Assert.Empty(result);
        }

        [Fact]
        public void HighRiskReport_SortsEqualScoresBySensorId()
        {
            var readings = new[]
            {
                MakeReading("S2", 9m),
                MakeReading("S1", 9m)
            };

            var result =
                ReadingReports.CreateHighRiskReport(readings);

            Assert.Equal(2, result.Count);
            Assert.Equal("S1", result[0].SensorId);
            Assert.Equal("S2", result[1].SensorId);
            Assert.Equal(result[0].RiskScore, result[1].RiskScore);
        }

        [Fact]
        public void SensorStatistics_CalculatesAggregatesCorrectly()
        {
            var readings = new[]
            {
                MakeReading("S1", 4m),
                MakeReading("S1", 6m),
                MakeReading("S2", -10m)
            };

            var result =
                ReadingReports.CreateSensorStatisticsReport(readings);

            var sensorS1 = result.Single(
                item => item.SensorId == "S1");

            Assert.Equal(2, sensorS1.ReadingCount);
            Assert.Equal(5m, sensorS1.AverageTemperature);
            Assert.Equal(6m, sensorS1.MaximumTemperature);
            Assert.Equal(10m, sensorS1.TotalTemperature);
        }

        [Fact]
        public void SensorStatistics_ReturnsEmptyForEmptyInput()
        {
            var result =
                ReadingReports.CreateSensorStatisticsReport(
                    Array.Empty<Reading>());

            Assert.Empty(result);
        }

        [Fact]
        public void CurriedPolicies_UseDifferentMargins()
        {
            var reading = new Reading(
                "S1",
                DateTimeOffset.Parse("2026-01-01T10:00:00Z"),
                StorageClass.Cold,
                7.0m);

            var createPolicy =
                CurriedRiskPolicy.CreateBoundaryPolicy();

            var cautiousPolicy = createPolicy(0.5m);
            var sensitivePolicy = createPolicy(1.5m);

            Assert.False(cautiousPolicy(reading));
            Assert.True(sensitivePolicy(reading));
        }

        [Fact]
        public void ReadingStream_UsesDeferredExecution()
        {
            var readings = new[]
            {
            MakeReading("S2", 5m),
            MakeReading("S1", 9m),
            MakeReading("S3", 7m)
            };

            int calls = 0;

            var result = ReadingStream.FilterReadings(
                readings,
                reading =>
                {
                    calls++;
                    return reading.Temperature > 6m;
                });

            // Creating the iterator does not execute the predicate.
            Assert.Equal(0, calls);

            using var enumerator = result.GetEnumerator();

            // First MoveNext processes elements until the first match.
            Assert.True(enumerator.MoveNext());
            Assert.Equal(2, calls);
            Assert.Equal("S1", enumerator.Current.SensorId);

            // The next MoveNext continues from where iteration stopped.
            Assert.True(enumerator.MoveNext());
            Assert.Equal(3, calls);
            Assert.Equal("S3", enumerator.Current.SensorId);

            Assert.False(enumerator.MoveNext());
        }


        [Fact]
        public void ReadingStream_ToArrayMaterializesResults()
        {
            var readings = new[]
            {
        MakeReading("S1", 9m),
        MakeReading("S2", 5m),
        MakeReading("S3", 7m)
    };

            int calls = 0;

            var deferred = ReadingStream.FilterReadings(
                readings,
                reading =>
                {
                    calls++;
                    return reading.Temperature > 6m;
                });

            Assert.Equal(0, calls);

            var snapshot = deferred.ToArray();

            Assert.Equal(3, calls);
            Assert.Equal(2, snapshot.Length);

            // Reading the snapshot does not execute the predicate again.
            var first = snapshot.ToList();

            Assert.Equal(3, calls);
            Assert.Equal(2, first.Count);
        }


        [Fact]
        public void ReadingFileStream_ReadsValidLinesAndSkipsInvalidLines()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                Guid.NewGuid() + ".txt");

            try
            {
                File.WriteAllLines(path, new[]
                {
            "S1|2026-01-01T10:00:00Z|Cold|5.2",
            "invalid line",
            "S2|2026-01-01T11:00:00Z|Frozen|-18.0"
        });

                var result =
                    ReadingFileStream.ReadValidReadings(path).ToList();

                Assert.Equal(2, result.Count);
                Assert.Equal("S1", result[0].SensorId);
                Assert.Equal("S2", result[1].SensorId);
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Fact]
        public void ReadingFileStream_DisposesFileAfterEarlyStop()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                Guid.NewGuid() + ".txt");

            try
            {
                File.WriteAllLines(path, new[]
                {
            "S1|2026-01-01T10:00:00Z|Cold|5.2",
            "S2|2026-01-01T11:00:00Z|Frozen|-18.0"
        });

                var readings = ReadingFileStream.ReadValidReadings(path);

                using (var enumerator = readings.GetEnumerator())
                {
                    Assert.True(enumerator.MoveNext());
                    Assert.Equal("S1", enumerator.Current.SensorId);
                }

                // The iterator has been disposed, so the file can be reopened.
                using var reopenedFile = File.OpenRead(path);

                Assert.True(reopenedFile.CanRead);
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }





        [Theory]
        [InlineData(9.0, 2.0, 8.0, 1.0)]
        [InlineData(5.0, 2.0, 8.0, 0.0)]
        [InlineData(-24.0, -22.0, -15.0, 2.0)]
        public void NativeRisk_CalculatesExpectedValues(
            double temperature,
            double safeMin,
            double safeMax,
            double expected)
        {
            var result = NativeRisk.Calculate(
                temperature, safeMin, safeMax);

            switch (result)
            {
                case Result<double, DomainError>.Success success:
                    Assert.Equal(expected, success.Value, 10);
                    break;

                case Result<double, DomainError>.Failure failure:
                    Assert.Fail(
                        $"Native calculation failed: " +
                        $"{failure.Error.Code}: {failure.Error.Message}");
                    break;

                default:
                    Assert.Fail("Unknown Result state.");
                    break;
            }
        }


        [Fact]
        public void NativeRisk_RejectsInvalidInput()
        {
            var result = NativeRisk.Calculate(
                double.NaN, 2.0, 8.0);

            var failure =
                Assert.IsType<Result<double, DomainError>.Failure>(result);

            Assert.Equal("INVALID_NATIVE_INPUT", failure.Error.Code);
        }

        [Fact]
        public void NativeRisk_ReturnsFailureWhenLibraryCannotBeLoaded()
        {
            var result = NativeRisk.CalculateUsing(
                5.0,
                2.0,
                8.0,
                (_, _, _) => throw new DllNotFoundException(
                    "Simulated missing library."));

            var failure =
                Assert.IsType<Result<double, DomainError>.Failure>(result);

            Assert.Equal("NATIVE_LIBRARY_NOT_FOUND", failure.Error.Code);
        }

        [Fact]
        public void NativeRiskWorkflow_ReturnsSuccessfulReport()
        {
            var readings = new[]
            {
        MakeReading("S1", 5m)
    };

            var result =
                ColdChainWorkflow.CreateNativeRiskReportForSensor(
                    readings,
                    "S1",
                    RiskPolicies.Standard);

            var success =
                Assert.IsType<Result<NativeRiskReport, DomainError>.Success>(
                    result);

            Assert.Equal("S1", success.Value.Reading.SensorId);
            Assert.Equal(0.0, success.Value.NativeRiskDistance);
            Assert.Empty(success.Value.PipelineReport.Items);
        }

        [Fact]
        public void NativeRiskWorkflow_ReturnsFailureWhenSensorIsMissing()
        {
            var readings = new[]
            {
        MakeReading("S1", 5m)
    };

            var result =
                ColdChainWorkflow.CreateNativeRiskReportForSensor(
                    readings,
                    "S99",
                    RiskPolicies.Standard);

            var failure =
                Assert.IsType<Result<NativeRiskReport, DomainError>.Failure>(
                    result);

            Assert.Equal("SENSOR_NOT_FOUND", failure.Error.Code);
        }

        [Fact]
        public void NativeRisk_ReturnsFailureWhenEntryPointIsMissing()
        {
            var result = NativeRisk.CalculateUsing(
                5.0,
                2.0,
                8.0,
                (_, _, _) => throw new EntryPointNotFoundException(
                    "Simulated missing entry point."));

            var failure =
                Assert.IsType<Result<double, DomainError>.Failure>(result);

            Assert.Equal("NATIVE_FUNCTION_NOT_FOUND", failure.Error.Code);
        }

        [Fact]
        public void NativeRisk_ReturnsFailureWhenLibraryFormatIsInvalid()
        {
            var result = NativeRisk.CalculateUsing(
                5.0,
                2.0,
                8.0,
                (_, _, _) => throw new BadImageFormatException(
                    "Simulated invalid library format."));

            var failure =
                Assert.IsType<Result<double, DomainError>.Failure>(result);

            Assert.Equal("NATIVE_LIBRARY_INVALID", failure.Error.Code);
        }

    }
}
