using UnityEngine;

// Public compatibility boundary: Season and WeatherType stay in the global namespace so
// existing gameplay/editor scripts do not need an all-at-once namespace migration.
public static class WeatherRules
{
    public static bool IsRain(WeatherType value) => value == WeatherType.Rainy || value == WeatherType.Storm;
    public static bool IsSnow(WeatherType value) => value == WeatherType.Snowy || value == WeatherType.SnowStorm;
    public static bool IsStorm(WeatherType value) => value == WeatherType.Storm || value == WeatherType.SnowStorm;
    public static bool IsThunderstorm(WeatherType value) => value == WeatherType.Storm;
    public static bool SameFamily(WeatherType a, WeatherType b) =>
        a == b || (IsRain(a) && IsRain(b)) || (IsSnow(a) && IsSnow(b));

    public static WeatherType ResolvePrecipitation(WeatherType value, Season season, float temperature, float freezingPoint)
    {
        if (!IsRain(value) && !IsSnow(value)) return value;

        bool shouldSnow = season != Season.Summer && temperature <= freezingPoint;
        return IsStorm(value)
            ? (shouldSnow ? WeatherType.SnowStorm : WeatherType.Storm)
            : (shouldSnow ? WeatherType.Snowy : WeatherType.Rainy);
    }
}
