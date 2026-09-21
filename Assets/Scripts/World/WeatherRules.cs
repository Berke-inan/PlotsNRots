using UnityEngine;

// Stable families keep precipitation rules out of visual and crop consumers.
public static class WeatherRules
{
    public static bool IsRain(WeatherType value) => value == WeatherType.Rainy || value == WeatherType.Storm;
    public static bool IsSnow(WeatherType value) => value == WeatherType.Snowy || value == WeatherType.SnowStorm;
    public static bool IsStorm(WeatherType value) => value == WeatherType.Storm || value == WeatherType.SnowStorm;
    public static bool SameFamily(WeatherType a, WeatherType b) => a == b || (IsRain(a) && IsRain(b)) || (IsSnow(a) && IsSnow(b));

    public static WeatherType ResolvePrecipitation(WeatherType value, Season season, float temperature, float freezingPoint)
    {
        if (!IsRain(value) && !IsSnow(value)) return value;
        bool snow = season != Season.Summer && temperature <= freezingPoint;
        return IsStorm(value) ? (snow ? WeatherType.SnowStorm : WeatherType.Storm)
            : (snow ? WeatherType.Snowy : WeatherType.Rainy);
    }
}
