using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ColdChainTests")]

namespace ColdChainMonitor;

public static class NativeRisk
{
    [DllImport(
        "NativeRisk.dll",
        EntryPoint = "calculate_risk",
        CallingConvention = CallingConvention.Cdecl)]
    private static extern double CalculateRiskNative(
        double temperature,
        double safeMin,
        double safeMax);

    public static Result<double, DomainError> Calculate(
        double temperature,
        double safeMin,
        double safeMax)
    {
        return CalculateUsing(
            temperature,
            safeMin,
            safeMax,
            CalculateRiskNative);
    }

    internal static Result<double, DomainError> CalculateUsing(
        double temperature,
        double safeMin,
        double safeMax,
        Func<double, double, double, double> adapter)
    {
        if (!double.IsFinite(temperature) ||
            !double.IsFinite(safeMin) ||
            !double.IsFinite(safeMax) ||
            safeMin > safeMax)
        {
            return Result<double, DomainError>.Fail(
                new DomainError(
                    "INVALID_NATIVE_INPUT",
                    "Invalid temperature or safe range."));
        }

        try
        {
            double result = adapter(
                temperature, safeMin, safeMax);

            return Result<double, DomainError>.Ok(result);
        }
        catch (DllNotFoundException ex)
        {
            return Result<double, DomainError>.Fail(
                new DomainError("NATIVE_LIBRARY_NOT_FOUND", ex.Message));
        }
        catch (EntryPointNotFoundException ex)
        {
            return Result<double, DomainError>.Fail(
                new DomainError("NATIVE_FUNCTION_NOT_FOUND", ex.Message));
        }
        catch (BadImageFormatException ex)
        {
            return Result<double, DomainError>.Fail(
                new DomainError("NATIVE_LIBRARY_INVALID", ex.Message));
        }
    }
}
