using System;
using System.Reflection;
using UnityEngine;

namespace Airburst
{
    internal static class ScopeRangefinderBridge
    {
        private const float MaxMeasurementAgeSeconds = 3f;

        private static bool _resolveAttempted;
        private static PropertyInfo _distanceProperty;
        private static PropertyInfo _timeProperty;

        internal static bool TryGetFreshDistance(out float meters)
        {
            meters = 0f;

            if (!_resolveAttempted)
            {
                _resolveAttempted = true;
                ResolveApi();
            }

            if (_distanceProperty == null || _timeProperty == null)
            {
                return false;
            }

            try
            {
                float distance = (float)_distanceProperty.GetValue(null);
                float measuredAt = (float)_timeProperty.GetValue(null);
                if (distance > 0f && Time.time - measuredAt <= MaxMeasurementAgeSeconds)
                {
                    meters = distance;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogWarning($"ScopeRangefinder API read failed, disabling integration: {ex.Message}");
                _distanceProperty = null;
                _timeProperty = null;
            }

            return false;
        }

        private static void ResolveApi()
        {
            Type apiType = Type.GetType("ScopeRangefinder.RangefinderApi, maschine-ScopeRangefinder");
            if (apiType == null)
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name == "maschine-ScopeRangefinder")
                    {
                        apiType = assembly.GetType("ScopeRangefinder.RangefinderApi");
                        break;
                    }
                }
            }

            if (apiType == null)
            {
                Plugin.LogSource.LogInfo("ScopeRangefinder not installed, airburst uses sight zeroing only.");
                return;
            }

            _distanceProperty = apiType.GetProperty("LastMeasuredDistanceMeters", BindingFlags.Public | BindingFlags.Static);
            _timeProperty = apiType.GetProperty("LastMeasurementTime", BindingFlags.Public | BindingFlags.Static);

            if (_distanceProperty == null || _timeProperty == null)
            {
                _distanceProperty = null;
                _timeProperty = null;
                Plugin.LogSource.LogWarning("ScopeRangefinder found but RangefinderApi contract mismatch, airburst uses sight zeroing only.");
                return;
            }

            Plugin.LogSource.LogInfo("ScopeRangefinder detected, airburst uses its measured distance when fresh.");
        }
    }
}
