using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Plots & Rots/Weather/Season Weather Profile")]
public sealed class SeasonWeatherProfile : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public WeatherType weather;
        [Min(0f)] public float weight;
        [Range(0f, 1f)] public float minimumIntensity;
        [Range(0f, 1f)] public float maximumIntensity;
    }

    [SerializeField] private Entry[] entries = Array.Empty<Entry>();

    [Header("Daily climate (Celsius)")]
    [SerializeField] private Vector2 temperatureRange = new Vector2(6f, 18f);
    [SerializeField, Min(0f)] private float maximumDailyTemperatureChange = 4f;
    [SerializeField] private float freezingPoint = 1f;
    [SerializeField, Range(1f, 4f)] private float persistenceMultiplier = 1.8f;

    public float FreezingPoint => freezingPoint;
    public float TypicalTemperature => (temperatureRange.x + temperatureRange.y) * .5f;

    public float SelectTemperature(float roll, float previous)
    {
        float min = Mathf.Min(temperatureRange.x, temperatureRange.y);
        float max = Mathf.Max(temperatureRange.x, temperatureRange.y);
        float target = Mathf.Lerp(min, max, Mathf.Clamp01(roll));
        float maxDelta = Mathf.Max(0f, maximumDailyTemperatureChange);
        return Mathf.Clamp(Mathf.MoveTowards(previous, target, maxDelta), min, max);
    }

    // Compatibility entry point used by older validation code.
    public WeatherType Select(float roll, float intensityRoll, out float intensity) =>
        SelectInternal(roll, intensityRoll, WeatherType.Sunny, Season.Spring, 12f, false, out intensity);

    public WeatherType SelectForDay(
        float roll,
        float intensityRoll,
        WeatherType previous,
        Season season,
        float temperature,
        out float intensity) =>
        SelectInternal(roll, intensityRoll, previous, season, temperature, true, out intensity);

    private WeatherType SelectInternal(
        float roll,
        float intensityRoll,
        WeatherType previous,
        Season season,
        float temperature,
        bool useClimate,
        out float intensity)
    {
        intensity = 0f;
        if (entries == null || entries.Length == 0) return WeatherType.Sunny;

        float total = 0f;
        foreach (Entry entry in entries)
            total += Weight(entry, previous, season, temperature, useClimate);

        if (total <= 0f) return WeatherType.Sunny;

        float remaining = Mathf.Clamp(roll, 0f, .9999999f) * total;
        foreach (Entry entry in entries)
        {
            remaining -= Weight(entry, previous, season, temperature, useClimate);
            if (remaining >= 0f) continue;

            float minI = Mathf.Clamp01(Mathf.Min(entry.minimumIntensity, entry.maximumIntensity));
            float maxI = Mathf.Clamp01(Mathf.Max(entry.minimumIntensity, entry.maximumIntensity));
            intensity = Mathf.Lerp(minI, maxI, Mathf.Clamp01(intensityRoll));
            return useClimate
                ? WeatherRules.ResolvePrecipitation(entry.weather, season, temperature, freezingPoint)
                : entry.weather;
        }

        return WeatherType.Sunny;
    }

    private float Weight(Entry entry, WeatherType previous, Season season, float temperature, bool useClimate)
    {
        if (!Enum.IsDefined(typeof(WeatherType), entry.weather)) return 0f;
        if (useClimate && season == Season.Summer && WeatherRules.IsSnow(entry.weather)) return 0f;

        float weight = Mathf.Max(0f, entry.weight);
        WeatherType resolved = useClimate
            ? WeatherRules.ResolvePrecipitation(entry.weather, season, temperature, freezingPoint)
            : entry.weather;

        return weight * (useClimate && WeatherRules.SameFamily(previous, resolved)
            ? persistenceMultiplier
            : 1f);
    }
}
