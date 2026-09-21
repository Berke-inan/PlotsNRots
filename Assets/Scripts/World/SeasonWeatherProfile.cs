using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Plots & Rots/Season Weather Profile")]
public class SeasonWeatherProfile : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public WeatherType weather;
        [Min(0)] public float weight;
        [Range(0, 1)] public float minimumIntensity;
        [Range(0, 1)] public float maximumIntensity;
    }
    [SerializeField] private Entry[] entries = Array.Empty<Entry>();
    [Header("Daily climate (Celsius)")]
    [SerializeField] private Vector2 temperatureRange = new Vector2(6, 18);
    [SerializeField, Min(0)] private float maximumDailyTemperatureChange = 4;
    [SerializeField] private float freezingPoint = 1;
    [SerializeField, Range(1, 4)] private float persistenceMultiplier = 1.8f;
    public float FreezingPoint => freezingPoint;
    public float TypicalTemperature => (temperatureRange.x + temperatureRange.y) * 0.5f;

    public float SelectTemperature(float roll, float previous)
    {
        float min = Mathf.Min(temperatureRange.x, temperatureRange.y);
        float max = Mathf.Max(temperatureRange.x, temperatureRange.y);
        float target = Mathf.Lerp(min, max, Mathf.Clamp01(roll));
        return Mathf.Clamp(Mathf.MoveTowards(previous, target, Mathf.Max(0, maximumDailyTemperatureChange)), min, max);
    }
    // Legacy deterministic entry point (no persistence or precipitation conversion).
    public WeatherType Select(float roll, float intensityRoll, out float intensity) =>
        SelectInternal(roll, intensityRoll, WeatherType.Sunny, Season.Spring, 12, false, out intensity);

    public WeatherType SelectForDay(float roll, float intensityRoll, WeatherType previous, Season season, float temperature, out float intensity) =>
        SelectInternal(roll, intensityRoll, previous, season, temperature, true, out intensity);

    private WeatherType SelectInternal(float roll, float intensityRoll, WeatherType previous, Season season, float temperature, bool useClimate, out float intensity)
    {
        intensity = 0;
        if (entries == null) return WeatherType.Sunny;
        float total = 0;
        foreach (var entry in entries) total += Weight(entry, previous, season, temperature, useClimate);
        if (total <= 0) return WeatherType.Sunny;
        float remaining = Mathf.Clamp(roll, 0, 0.9999999f) * total;
        foreach (var entry in entries)
        {
            remaining -= Weight(entry, previous, season, temperature, useClimate);
            if (remaining >= 0) continue;
            intensity = Mathf.Lerp(Mathf.Clamp01(Mathf.Min(entry.minimumIntensity, entry.maximumIntensity)),
                Mathf.Clamp01(Mathf.Max(entry.minimumIntensity, entry.maximumIntensity)), Mathf.Clamp01(intensityRoll));
            return useClimate ? WeatherRules.ResolvePrecipitation(entry.weather, season, temperature, freezingPoint) : entry.weather;
        }
        return WeatherType.Sunny;
    }
    private float Weight(Entry entry, WeatherType previous, Season season, float temperature, bool useClimate)
    {
        if (!Enum.IsDefined(typeof(WeatherType), entry.weather)) return 0;
        if (useClimate && season == Season.Summer && WeatherRules.IsSnow(entry.weather)) return 0;
        float weight = Mathf.Max(0, entry.weight);
        var resolved = useClimate ? WeatherRules.ResolvePrecipitation(entry.weather, season, temperature, freezingPoint) : entry.weather;
        return weight * (useClimate && WeatherRules.SameFamily(previous, resolved) ? persistenceMultiplier : 1);
    }
}
