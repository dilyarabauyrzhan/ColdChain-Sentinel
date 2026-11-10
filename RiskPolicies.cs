using System;

namespace ColdChainMonitor
{
    public static class RiskPolicies
    {
        public static readonly Func<Reading, bool> Standard =
            r => ColdChainPipeline.IsOutsideSafeRange(r);

        public static readonly Func<Reading, bool> Strict =
            r => ColdChainPipeline.IsOutsideSafeRange(r)
                 || ColdChainPipeline.IsNearBoundary(r);
    }
}
