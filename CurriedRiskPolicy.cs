
using System;

namespace ColdChainMonitor;

public static class CurriedRiskPolicy
{
    public static Func<decimal, Func<Reading, bool>> CreateBoundaryPolicy()
    {
        return margin =>
        {
            if (margin < 0)
                throw new ArgumentOutOfRangeException(nameof(margin));

            return reading =>
            {
                ArgumentNullException.ThrowIfNull(reading);

                return ColdChainPipeline.IsOutsideSafeRange(reading)
                    || ColdChainPipeline.IsNearBoundary(reading, margin);
            };
        };
    }
}
