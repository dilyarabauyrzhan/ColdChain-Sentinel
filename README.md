# Assignment 8: Extension Methods and LINQ

**Student:** Dilyara Bauyrzhan  
**Group:** IT-2505  
**Course:** Introduction to Functional Programming  
**Project:** ColdChainMonitor

## 1. Project Overview

This assignment extends the existing Cold Chain Monitoring application developed during Assignments 5–7.

The application processes temperature readings, validates domain data, applies configurable risk policies, generates reports, handles operational errors, and demonstrates functional programming concepts.

Assignment 8 introduces two custom extension methods, LINQ-based reports, currying and partial application, deferred execution, resource management, and native C library integration through P/Invoke.

The existing application architecture, functional pipeline, explicit error handling, and previous functionality are preserved.

## 2. Technologies and Environment

- C#
- .NET 10
- xUnit
- Visual Studio 2026 Community
- Microsoft Visual C++ Compiler (MSVC)
- Native C library
- P/Invoke (`DllImport`)
- JSON serialization

**Target platform:** Windows x64  
**Native library:** `NativeRisk.dll`  
**Calling convention:** Cdecl  
**Compiler:** Microsoft Visual C++ (`cl.exe`)  
**MSVC compiler version:** `19.51.36260`  
**Architecture:** x64

## 3. Project Structure

The solution contains two projects:

- `ColdChainMonitor` — the main console application.
- `ColdChainTests` — the automated test project.

Important implementation files:

| File | Description |
|---|---|
| `ReadingExtensions.cs` | Custom extension methods |
| `ReadingReports.cs` | LINQ-based reports |
| `CurriedRiskPolicy.cs` | Curried risk-policy factory |
| `ReadingStream.cs` | Deferred processing with `yield return` |
| `ReadingFileStream.cs` | Resource-safe file iteration |
| `NativeRisk.cs` | Native C function adapter |
| `ColdChainWorkflow.cs` | Result-based application workflows |
| `ColdChainPipeline.cs` | Reading selection, risk calculation, and report generation |
| `Program.cs` | Application entry point and demonstrations |
| `Assignment8Tests.cs` | Assignment 8 automated tests |
| `Native/native_risk.c` | Native C implementation |
| `Native/native_risk.h` | Native C header |
| `Native/build.bat` | Native library build script |

## 4. Custom Extension Methods

Two custom extension methods are implemented and used by the application.

### 4.1 ForSensor

**Receiver:** `IEnumerable<Reading>`  
**Return type:** `IEnumerable<Reading>`

Filters temperature readings by sensor identifier.

The method belongs to the readings collection because filtering by sensor is a reusable operation on a sequence of readings.

### 4.2 RecordedBetween

**Receiver:** `IEnumerable<Reading>`  
**Return type:** `IEnumerable<Reading>`

Filters readings recorded within a specified time interval.

The method belongs to the readings collection because it expresses a reusable time-based query over reading data.

Both methods are called from `Program.cs`, and their results are displayed in the application.

## 5. LINQ Reports

The application produces two reports using actual validated reading data.

### 5.1 High-Risk Report

**Implementation:** `ReadingReports.CreateHighRiskReport`

**Input:** Validated temperature readings.

**Purpose:** Identify readings that represent high-risk temperature conditions.

**Filtering:** Selects readings classified as high risk.

**Output fields:**

- Sensor ID
- Timestamp
- Temperature
- Risk level
- Risk score

The report uses filtering, projection, and ordering. Equal risk scores are resolved using the sensor identifier as a secondary sorting key.

### 5.2 Sensor Statistics Report

**Implementation:** `ReadingReports.CreateSensorStatisticsReport`

**Input:** Validated temperature readings.

**Purpose:** Provide a summary of temperature data for each sensor.

**Grouping:** Readings are grouped by sensor identifier.

**Output fields:**

- Sensor ID
- Reading count
- Average temperature
- Maximum temperature
- Total temperature

The report uses grouping and aggregate operations to calculate statistics for each sensor.

Both reports are implemented separately from console output and are called from `Program.cs`.

The query transformations and calculations are kept separate from presentation.

## 6. Currying and Partial Application

**Implementation:** `CurriedRiskPolicy.cs`

The method `CreateBoundaryPolicy()` returns a function that accepts a configuration parameter and produces another function that evaluates a temperature reading.

Its conceptual structure is:

`Configuration -> (Reading -> bool)`

The application creates two configured policies through partial application:

- **Cautious policy:** margin of `0.5`.
- **Sensitive policy:** margin of `1.5`.

Both policies are applied to the same validated dataset.

The different configurations can produce different selection results.

The configured sensitive policy is also passed into `ColdChainWorkflow.CreateReportForSensor`.

This design allows the workflow to receive its policy as a function parameter instead of relying on mutable global configuration.

## 7. Deferred Execution and Materialization

**Implementation:** `ReadingStream.cs`

The method `FilterReadings` returns an `IEnumerable<Reading>` and uses `yield return` to produce matching readings.

The iterator demonstrates deferred execution:

- Creating the enumerable does not execute the filtering logic.
- Enumeration begins when the consumer requests elements.
- Processing continues as elements are requested.

The tests use a counter to verify when the predicate is executed without introducing diagnostic side effects into the production calculation.

### Materialization

The application can materialize a deferred sequence using:

- `ToArray()`
- `ToList()`

Materialization evaluates the query and stores the resulting elements in a collection.

Reusing the resulting array or list does not execute the original iterator again.

By contrast, enumerating the original deferred sequence multiple times may execute its filtering logic repeatedly.

Materialization is useful when a stable snapshot is required for repeated processing or reporting.

## 8. Resource Management

**Implementation:** `ReadingFileStream.cs`

The application uses a file-backed iterator to read valid temperature readings.

The iterator uses `StreamReader` inside a `using` scope.

The iterator owns the file reader while enumeration is active.

The resource remains available while the iterator is paused between yielded elements.

The file is disposed when:

- Enumeration completes normally.
- An exception interrupts enumeration.
- The consumer stops early and disposes the enumerator.

In `Program.cs`, the application reads the first available reading and then stops enumeration.

The enumerator is enclosed in a `using` statement, ensuring that the iterator is disposed and its file resource is released.

The automated tests verify that the file can be reopened after early termination.

## 9. Native C Library Integration

The project includes a native C library integrated into the C# application.

### 9.1 Native implementation

**Source files:**

- `Native/native_risk.c`
- `Native/native_risk.h`

The library exports the function:

`calculate_risk`

The function calculates the distance of a temperature from a safe temperature interval.

### 9.2 Function contract

**Input parameters:**

- `temperature` — temperature value (`double`).
- `safeMin` — minimum safe temperature (`double`).
- `safeMax` — maximum safe temperature (`double`).

**Return type:** `double`

The function returns:

- `0` when the temperature is inside the safe interval.
- The distance to the nearest violated boundary when the temperature is outside the interval.

**Input validation:**

The C# adapter checks that all numeric inputs are finite and that the minimum safe value does not exceed the maximum safe value.

Invalid inputs return a `Result` failure with the error code:

`INVALID_NATIVE_INPUT`

### 9.3 Manually verified example

For the following input:

`calculate_risk(9.0, 2.0, 8.0)`

The temperature is one degree above the maximum safe temperature.

Expected result:

`9.0 - 8.0 = 1.0`

The native function therefore returns `1.0`.

### 9.4 C# P/Invoke binding

**Implementation:** `NativeRisk.cs`

The C# application declares the native function using `DllImport`.

The binding specifies:

- Library name: `NativeRisk.dll`
- Entry point: `calculate_risk`
- Calling convention: `CallingConvention.Cdecl`
- Parameters: three `double` values
- Return type: `double`

The adapter wraps the native call and returns:

`Result<double, DomainError>`

### 9.5 Native error handling

The adapter translates native loading and binding exceptions into explicit domain errors.

| Exception | Result error code |
|---|---|
| `DllNotFoundException` | `NATIVE_LIBRARY_NOT_FOUND` |
| `EntryPointNotFoundException` | `NATIVE_FUNCTION_NOT_FOUND` |
| `BadImageFormatException` | `NATIVE_LIBRARY_INVALID` |

This prevents expected native integration failures from being exposed as unhandled application exceptions.

## 10. Native Workflow Integration

**Implementation:** `ColdChainWorkflow.CreateNativeRiskReportForSensor`

The native function is integrated into an actual application workflow.

The workflow performs the following operations:

1. Finds the requested sensor.
2. Validates the reading.
3. Checks whether the operation is allowed.
4. Obtains the safe temperature range for the storage class.
5. Calls `NativeRisk.Calculate`.
6. Uses the returned native value to create a `NativeRiskReport`.
7. Includes the corresponding C# pipeline report.

The workflow returns:

`Result<NativeRiskReport, DomainError>`

A missing sensor or invalid reading produces an explicit failure.

The native result is used as part of the actual workflow rather than being limited to a standalone demonstration.

## 11. Native Build and Loading Instructions

### Prerequisites

- Windows x64
- Visual Studio 2026 Community with C++ build tools
- Microsoft Visual C++ compiler (`cl.exe`)
- .NET 10 SDK

### Build procedure

Open the **x64 Native Tools Command Prompt for Visual Studio**.

Navigate to the native source directory:

```bat
cd ColdChainMonitor\Native
```

Run the build script:

```bat
build.bat
```

The script uses the Microsoft C compiler to build the native DLL from `native_risk.c`.

The native library is compiled as a Windows x64 DLL.

The resulting DLL is copied into the application and test output directories:

```text
ColdChainMonitor/bin/Debug/net10.0/NativeRisk.dll
ColdChainTests/bin/Debug/net10.0/NativeRisk.dll
```

These locations allow both the application and the test project to resolve the native library during execution.

The current build script targets the Debug configuration and .NET 10 output directories.

### Compiler version

The compiler version can be checked by running:

```bat
cl
```

The development environment used **Visual Studio 2026 Community** with **Microsoft Visual C++ compiler version `19.51.36260`** in the x64 Native Tools environment.

The compiler version and architecture were verified using the command output.

### Integration difficulty and resolution

During development, the native tests initially failed because the test project could not locate `NativeRisk.dll`.

The issue was resolved by updating `build.bat` to copy the generated DLL into both the application and test output directories.

After the change, the real native calculation tests and the integrated workflow tests passed.

## 12. Error Handling and File Boundaries

The application preserves the explicit error-handling architecture developed in previous assignments.

Expected domain failures are represented using `Result` values.

Examples include:

- `SENSOR_NOT_FOUND`
- `INVALID_SENSOR_ID`
- `INVALID_STORAGE_CLASS`
- `INVALID_TEMPERATURE`
- `INVALID_TIMESTAMP`

File operations also handle operational errors.

The application demonstrates:

- Missing JSON files.
- Malformed JSON content.

These errors are handled at the file I/O boundary and are returned as explicit failures.

## 13. Automated Tests

The `ColdChainTests` project uses xUnit.

**Latest reported test result: 51 passed, 0 failed.**

The automated test suite covers previous assignment functionality and the new Assignment 8 requirements.

### Extension methods

- Filtering readings by sensor.
- Case-insensitive sensor matching.
- Empty results when no sensor matches.
- Validation of invalid sensor identifiers.
- Time-range filtering and boundary inclusion.
- Rejection of reversed time intervals.

### LINQ reports

- Empty input handling.
- No matching high-risk readings.
- Deterministic ordering when risk scores are equal.
- Correct sensor grouping and aggregate calculations.

### Configured functions

- Different configured risk margins.
- Application of both policies to the same reading.

### Deferred execution

- No processing when the iterator is created.
- Processing during enumeration.
- Correct behavior of `MoveNext()`.
- Materialization using `ToArray()`.
- Predictable reuse of materialized results.

### Resource lifetime

- Reading valid file records.
- Skipping malformed lines.
- Releasing the file after early iterator termination.

### Native integration

- Real native calculations against manually verified expected values.
- Invalid native input handling.
- Missing native library failure.
- Missing native entry point failure.
- Invalid native library format failure.
- Successful native workflow integration.
- Missing sensor failure in the native workflow.

Run the automated tests with:

```bash
dotnet test ColdChainTests/ColdChainTests.csproj
```

## 14. Requirement-to-File Mapping

| Requirement | Implementation |
|---|---|
| Two custom extension methods | `ReadingExtensions.cs` |
| Extension method usage | `Program.cs` |
| High-risk LINQ report | `ReadingReports.cs` |
| Sensor statistics report | `ReadingReports.cs` |
| Currying and partial application | `CurriedRiskPolicy.cs` |
| Configured policy integration | `ColdChainWorkflow.cs`, `Program.cs` |
| Deferred execution | `ReadingStream.cs` |
| Materialization | `ReadingStream.cs`, `Program.cs`, `Assignment8Tests.cs` |
| Resource management | `ReadingFileStream.cs` |
| Native C implementation | `Native/native_risk.c` |
| Native C header | `Native/native_risk.h` |
| Native build instructions | `Native/build.bat` |
| P/Invoke adapter | `NativeRisk.cs` |
| Native workflow integration | `ColdChainWorkflow.cs` |
| Automated tests | `ColdChainTests/Assignment8Tests.cs` |
| Application demonstrations | `Program.cs` |

## 15. Memory and Resource Management Notes

The application uses both deferred sequences and materialized collections.

Deferred queries avoid immediately allocating a collection for all resulting elements. However, an iterator and its closures can retain references to their source data while they are in use.

Enumerating a deferred query repeatedly may repeat its filtering operations.

Materialization through `ToArray()` or `ToList()` creates a stable snapshot. This requires memory for the resulting collection but avoids repeating the original query when the snapshot is reused.

The application uses materialization where a stable collection is useful for subsequent processing and reporting.

The file-backed iterator retains its file resource while enumeration is active. Its `using` scope ensures that the resource is released when iteration finishes or when the enumerator is disposed early.

The application does not implement long-lived caches or event subscriptions. Therefore, it does not introduce additional retention through those mechanisms.

The design uses lazy iteration when streaming and early termination are useful, and materialization when predictable reuse of results is required.

## 16. Running the Application

### Build the solution

From the directory containing `ColdChainMonitor.slnx`:

```bash
dotnet build ColdChainMonitor.slnx
```

### Run the application

```bash
dotnet run --project ColdChainMonitor/ColdChainMonitor.csproj
```

The application requires the `readings.txt` input file in its working directory.

The console application demonstrates:

- Reading import and validation.
- Standard and strict risk policies.
- Functional workflow composition.
- Currying and partial application.
- Deferred execution.
- File resource management.
- Native C calculations.
- Native risk workflow integration.
- Extension methods.
- High-risk and sensor statistics reports.
- Missing-file and malformed JSON scenarios.

### Run automated tests

```bash
dotnet test ColdChainTests/ColdChainTests.csproj
```

## 17. Student Contribution

**Dilyara Bauyrzhan — IT-2505**

Implemented and integrated the Assignment 8 functionality, including:

- Custom extension methods.
- LINQ-based reports.
- Curried risk policies and partial application.
- Deferred reading processing and materialization.
- File-backed iteration and resource disposal.
- Native C library implementation and build integration.
- C# P/Invoke adapter and error translation.
- Native risk workflow integration.
- Automated tests and project documentation.

## 18. Conclusion

Assignment 8 extends the Cold Chain Monitoring application with reusable extension methods, LINQ reports, configurable functional policies, deferred execution, resource-safe streaming, and native C interoperability.

The native function is integrated into the existing C# workflow through a validated adapter that translates native integration failures into explicit Result outcomes.

Automated tests verify the behavior of the new functionality, including real native calculations, controlled failure scenarios, and resource disposal.

The project continues the same application developed in Assignments 5–7 and preserves its existing processing pipeline and explicit error-handling approach.