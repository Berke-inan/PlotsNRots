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

    public WeatherType Select(float roll, float intensityRoll, out float intensity)
    {
        if (entries == null) { intensity = 0; return WeatherType.Sunny; }
        float total = 0;
        foreach (var entry in entries)
            if (Enum.IsDefined(typeof(WeatherType), entry.weather)) total += Mathf.Max(0, entry.weight);
        if (total <= 0) { intensity = 0; return WeatherType.Sunny; }
        float remaining = Mathf.Clamp(roll, 0, 0.9999999f) * total;
        foreach (var entry in entries)
        {
            if (!Enum.IsDefined(typeof(WeatherType), entry.weather)) continue;
            remaining -= Mathf.Max(0, entry.weight);
            if (remaining >= 0) continue;
            float min = Mathf.Clamp01(Mathf.Min(entry.minimumIntensity, entry.maximumIntensity));
            float max = Mathf.Clamp01(Mathf.Max(entry.minimumIntensity, entry.maximumIntensity));
            intensity = Mathf.Lerp(min, max, Mathf.Clamp01(intensityRoll));
            return entry.weather;
        }
        intensity = 0;
        return WeatherType.Sunny;
    }
}
