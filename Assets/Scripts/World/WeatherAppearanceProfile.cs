using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Plots & Rots/Weather Appearance Profile")]
public class WeatherAppearanceProfile : ScriptableObject
{
    [Serializable] public struct Entry { public WeatherType weather; public WeatherVisuals visuals; }
    [SerializeField] private Entry[] entries = Array.Empty<Entry>();
    public bool TryGet(WeatherType weather, out WeatherVisuals visuals)
    {
        if (entries != null)
            foreach (var entry in entries)
                if (entry.weather == weather) { visuals = entry.visuals; return true; }
        visuals = default;
        return false;
    }
}
