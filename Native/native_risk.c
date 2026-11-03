
#include "native_risk.h"

double calculate_risk(double temperature, double safe_min, double safe_max)
{
    if (temperature < safe_min)
        return safe_min - temperature;

    if (temperature > safe_max)
        return temperature - safe_max;

    return 0.0;
}
