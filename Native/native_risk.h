
#ifndef NATIVE_RISK_H
#define NATIVE_RISK_H

#ifdef _WIN32
#define API __declspec(dllexport)
#else
#define API
#endif

API double calculate_risk(double temperature, double safe_min, double safe_max);

#endif
