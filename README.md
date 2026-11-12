# ColdChain Sentinel
### Functional Cold Chain Monitoring System

**Student:** Dilyara Bauyrzhan  
**Group:** IT-2505  
**Course:** Introduction to Functional Programming  
**Academic Year:** 2026–2027  
**Project Type:** Individual Final Project

---

## 1. Project Overview

ColdChain Sentinel is a C# console application designed to process and monitor temperature readings in cold-chain storage environments.

The application works with temperature readings associated with sensors and storage classes. It validates incoming data, applies configurable risk policies, processes readings through functional workflows, and generates reports that help identify potentially unsafe temperature conditions.

The project demonstrates functional programming concepts studied in the Introduction to Functional Programming course, including functional composition, higher-order functions, explicit error handling, LINQ queries, lazy evaluation, and resource management.

The application also integrates a native C library through P/Invoke to perform a temperature risk calculation.

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
**MSVC compiler version:** `19.51.36260`  
**Architecture:** x64  
**.NET SDK version:** 11.0.100-rc.3.26522.148

## 3. Project Structure

The solution contains two projects:

- `ColdChainMonitor` — the main console application.
- `ColdChainTests` — the automated test project.

Important implementation files:

| File | Responsibility |
|---|---|
| `Reading.cs` | Temperature reading domain model |
| `ColdChainPipeline.cs` | Reading processing and report pipeline |
| `ColdChainProcessor.cs` | Reading processing logic |
| `ColdChainWorkflow.cs` | Application workflows |
| `ReadingExtensions.cs` | Custom extension methods |
| `ReadingReports.cs` | LINQ-based reports |
| `CurriedRiskPolicy.cs` | Configurable curried risk policies |
| `ReadingStream.cs` | Deferred processing with `yield return` |
| `ReadingFileStream.cs` | Resource-safe file iteration |
| `NativeRisk.cs` | Native C function adapter |
| `Program.cs` | Application entry point and demonstrations |
| `Native/native_risk.c` | Native C implementation |
| `Native/native_risk.h` | Native C header |
| `Native/build.bat` | Native library build script |
| `ColdChainTests/` | Automated tests |

## 4. Main Application Functionality

The application demonstrates the following functionality:

- Processing and validating temperature readings.
- Applying configurable risk policies.
- Filtering readings by sensor and recording time.
- Generating high-risk and sensor statistics reports.
- Processing readings through functional workflows.
- Handling expected domain and operational errors.
- Reading data through lazy iterators.
- Managing file resources safely.
- Performing native temperature risk calculations through P/Invoke.

The application uses sample reading data supplied with the project.

## 5. Functional Programming Design

### 5.1 Functional Core

The application separates domain processing and calculations from console interaction and file operations.

This separation makes the core logic easier to test independently and reduces dependencies on external side effects.

### 5.2 Functional Pipeline

`ColdChainPipeline.cs` contains the reading processing and report generation logic.

The application uses composed processing operations to transform and evaluate temperature readings.

### 5.3 Higher-Order Functions and Currying

**Implementation:** `CurriedRiskPolicy.cs`

The method `CreateBoundaryPolicy()` returns a function that accepts a configuration parameter and produces another function that evaluates a temperature reading.

Conceptually:

`Configuration -> (Reading -> bool)`

The application creates two configured policies:

- **Cautious policy:** margin of `0.5`.
- **Sensitive policy:** margin of `1.5`.

Both policies can be applied to the same validated dataset.

The configured policy can also be passed into an application workflow instead of relying on mutable global configuration.

### 5.4 Explicit Error Handling

The application uses explicit result-based error handling.

Expected domain failures are represented through `Result` values rather than being treated as successful calculations.

Examples of domain error codes include:

- `SENSOR_NOT_FOUND`
- `INVALID_SENSOR_ID`
- `INVALID_STORAGE_CLASS`
- `INVALID_TEMPERATURE`
- `INVALID_TIMESTAMP`

Operational exceptions are handled at relevant application boundaries.

## 6. Queries and Reports

The application includes two custom extension methods.

### 6.1 ForSensor

**Receiver:** `IEnumerable<Reading>`  
**Return type:** `IEnumerable<Reading>`

Filters temperature readings by sensor identifier.

### 6.2 RecordedBetween

**Receiver:** `IEnumerable<Reading>`  
**Return type:** `IEnumerable<Reading>`

Filters readings recorded within a specified time interval.

### 6.3 High-Risk Report

**Implementation:** `ReadingReports.CreateHighRiskReport`

Identifies readings classified as high risk.

The report includes sensor identifiers, timestamps, temperatures, risk levels, and risk scores.

It uses filtering, projection, and ordering with a secondary sorting key.

### 6.4 Sensor Statistics Report

**Implementation:** `ReadingReports.CreateSensorStatisticsReport`

Groups readings by sensor and calculates statistics such as:

- Reading count.
- Average temperature.
- Maximum temperature.
- Total temperature.

The reports use LINQ operations and are separated from console presentation.

## 7. Deferred Execution and Resource Management

### 7.1 Deferred Execution

**Implementation:** `ReadingStream.cs`

The method `FilterReadings` returns an `IEnumerable<Reading>` and uses `yield return`.

Creating the enumerable does not immediately execute the filtering logic. Processing begins when the sequence is enumerated.

The application demonstrates materialization through `ToArray()` and explains the difference between deferred sequences and reusable snapshots.

### 7.2 Resource Management

**Implementation:** `ReadingFileStream.cs`

The application uses a file-backed iterator with `StreamReader` inside a `using` scope.

The file resource is released when enumeration completes, an exception occurs, or the consumer stops early and disposes the enumerator.

Automated tests verify that the file can be reopened after early iterator termination.

## 8. Native C Library Integration

The project includes a native C library integrated into the C# application.

### 8.1 Native Implementation

Source files:

- `Native/native_risk.c`
- `Native/native_risk.h`

The library exports the function `calculate_risk`.

It calculates the distance of a temperature from a safe temperature interval.

### 8.2 Function Contract

Input parameters:

- `temperature` — temperature value (`double`).
- `safeMin` — minimum safe temperature (`double`).
- `safeMax` — maximum safe temperature (`double`).

Return type: `double`.

The function returns:

- `0` when the temperature is inside the safe interval.
- The distance to the nearest violated boundary when the temperature is outside the interval.

### 8.3 Verified Example

Input:

`calculate_risk(9.0, 2.0, 8.0)`

Expected result:

`9.0 - 8.0 = 1.0`

The native function therefore returns `1.0`.

### 8.4 C# P/Invoke Adapter

**Implementation:** `NativeRisk.cs`

The C# application declares the native function using `DllImport`.

The binding specifies:

- Library: `NativeRisk.dll`
- Entry point: `calculate_risk`
- Calling convention: `CallingConvention.Cdecl`
- Three `double` parameters
- `double` return type

The adapter validates numeric inputs and returns:

`Result<double, DomainError>`

Native loading and binding failures are translated into explicit errors:

| Exception | Error code |
|---|---|
| `DllNotFoundException` | `NATIVE_LIBRARY_NOT_FOUND` |
| `EntryPointNotFoundException` | `NATIVE_FUNCTION_NOT_FOUND` |
| `BadImageFormatException` | `NATIVE_LIBRARY_INVALID` |

### 8.5 Native Workflow Integration

**Implementation:** `ColdChainWorkflow.CreateNativeRiskReportForSensor`

The native calculation is used within an application workflow.

The workflow finds the requested sensor, validates the reading, checks whether the operation is allowed, obtains the safe temperature range, calls the native function, and uses the returned value to create a report.

The native result is therefore part of application functionality rather than an isolated demonstration.

## 9. Building the Native Library

### Prerequisites

- Windows x64
- Visual Studio 2026 Community with C++ build tools
- Microsoft Visual C++ compiler (`cl.exe`)
- .NET 10 SDK

### Build procedure

Open the **x64 Native Tools Command Prompt for Visual Studio**.

From the project root, navigate to the native directory:

```bat
cd Native
```

Run:

```bat
build.bat
```

The script compiles `native_risk.c` into a Windows x64 DLL and copies the resulting library into the application and test output directories.

The compiler version can be checked with:

```bat
cl
```

The development environment used MSVC compiler version `19.51.36260` for x64.

## 10. Automated Testing

The `ColdChainTests` project uses xUnit.

The previously recorded test run reported **51 passed, 0 failed**. The final test result should be verified by running the tests again before submission.

The test suite covers:

- Extension method behavior and boundary cases.
- LINQ reports and deterministic ordering.
- Configured risk policies.
- Deferred execution and materialization.
- File iteration and early resource disposal.
- Native calculations and invalid inputs.
- Native library loading and binding failures.
- Native workflow integration.
- Missing sensor behavior.

Run the tests using:

```bash
dotnet test ColdChainTests/ColdChainTests.csproj
```

## 11. Build and Run Instructions

### Build the solution

From the directory containing `ColdChainMonitor.slnx`:

```bash
dotnet build ColdChainMonitor.slnx
```

### Run the application

```bash
dotnet run --project ColdChainMonitor.csproj
```

The application uses the supplied `readings.txt` sample data file.

### Run automated tests

```bash
dotnet test ColdChainTests/ColdChainTests.csproj
```

The native DLL must be built using the provided `Native/build.bat` script before running functionality that requires native integration.

## 12. Memory and Resource Management

The application uses deferred sequences when lazy processing is useful and materialized collections when a reusable snapshot is required.

Deferred queries can retain references to their source data while being used, and repeated enumeration can repeat the original processing operations.

Materialization through `ToArray()` or `ToList()` creates a stable collection that can be reused without re-executing the original iterator.

The file-backed iterator owns its `StreamReader` during enumeration and releases it when the iterator is disposed.

The application does not implement long-lived caches or event subscriptions.

## 13. Requirement Mapping

| Area | Relevant implementation |
|---|---|
| Domain models | `Reading.cs`, related domain types |
| Processing pipeline | `ColdChainPipeline.cs` |
| Application workflows | `ColdChainWorkflow.cs` |
| Configurable functions | `CurriedRiskPolicy.cs` |
| Extension methods | `ReadingExtensions.cs` |
| LINQ reports | `ReadingReports.cs` |
| Lazy processing | `ReadingStream.cs` |
| Resource management | `ReadingFileStream.cs` |
| Native C implementation | `Native/native_risk.c` |
| Native C header | `Native/native_risk.h` |
| Native build | `Native/build.bat` |
| P/Invoke adapter | `NativeRisk.cs` |
| Automated tests | `ColdChainTests/` |

## 14. Individual Contribution

**Dilyara Bauyrzhan — IT-2505**

The project is developed individually.

The implementation work includes the development and integration of the cold-chain monitoring functionality, functional processing components, extension methods, LINQ reports, configurable risk policies, lazy reading processing, resource-safe file iteration, native C integration, automated tests, and project documentation.

The individual contribution report provides a more detailed explanation of the implemented features, source files, methods, tests, and technical decisions.

## 15. Repository Information

**Project:** ColdChain Sentinel  
**Repository:** https://github.com/dilyarabauyrzhan/ColdChain-Sentinel  
**Branch:** `main`  


The submitted project archive must correspond to the final commit in this repository.

## 16. Conclusion

ColdChain Sentinel demonstrates a functional approach to cold-chain temperature monitoring using C# and a native C component.

The application combines temperature reading processing, configurable risk policies, LINQ-based reports, lazy evaluation, resource management, explicit error handling, and native interoperability.

The project is designed to demonstrate the practical application of functional programming concepts through a coherent domain-specific application.
