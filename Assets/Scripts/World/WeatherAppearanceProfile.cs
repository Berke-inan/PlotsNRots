using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Plots & Rots/Weather/Weather Appearance Profile")]
public sealed class WeatherAppearanceProfile : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public WeatherType weather;
        public WeatherVisuals visuals;
    }

    [SerializeField] private Entry[] entries = Array.Empty<Entry>();

    public bool TryGet(WeatherType weather, out WeatherVisuals visuals)
    {
        if (entries != null)
        {
            foreach (Entry entry in entries)
            {
                if (entry.weather != weather) continue;
                visuals = entry.visuals;
                return true;
            }
        }

        visuals = default;
        return false;
    }

    [ContextMenu("Reset To Recommended PC Defaults")]
    private void ResetRecommended()
    {
        WeatherType[] values = (WeatherType[])Enum.GetValues(typeof(WeatherType));
        entries = new Entry[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            entries[i] = new Entry
            {
                weather = values[i],
                visuals = WeatherVisualsDefaults.For(values[i])
            };
        }
    }
}

public static class WeatherVisualsDefaults
{
    public static WeatherVisuals For(WeatherType weather)
    {
        switch (weather)
        {
            case WeatherType.Sunny:
                return New(
                    top: new Color(.23f, .52f, .80f), bottom: new Color(.68f, .84f, .94f),
                    cloud: new Color(1f, .99f, .96f), coverage: .20f,
                    fog: new Color(.72f, .84f, .91f), fogDensity: .00045f,
                    darkness: 0f, direct: 1f, ambient: 1f, wind: .12f,
                    overcast: .02f, cirrus: .08f, thickness: .26f);

            case WeatherType.PartlyCloudy:
                return New(
                    top: new Color(.25f, .53f, .79f), bottom: new Color(.67f, .82f, .91f),
                    cloud: new Color(.97f, .98f, 1f), coverage: .48f,
                    fog: new Color(.68f, .80f, .89f), fogDensity: .0014f,
                    darkness: .05f, direct: .93f, ambient: .96f, wind: .28f,
                    overcast: .08f, cirrus: .10f, thickness: .40f);

            case WeatherType.Cloudy:
                return New(
                    top: new Color(.38f, .49f, .59f), bottom: new Color(.63f, .71f, .77f),
                    cloud: new Color(.82f, .87f, .91f), coverage: .62f,
                    fog: new Color(.64f, .71f, .76f), fogDensity: .0035f,
                    darkness: .16f, direct: .76f, ambient: .88f, wind: .40f,
                    overcast: .28f, cirrus: .05f, thickness: .58f);

            case WeatherType.Overcast:
                return New(
                    top: new Color(.34f, .40f, .47f), bottom: new Color(.55f, .61f, .66f),
                    cloud: new Color(.68f, .73f, .79f), coverage: .76f,
                    fog: new Color(.54f, .61f, .67f), fogDensity: .006f,
                    darkness: .30f, direct: .60f, ambient: .76f, wind: .48f,
                    overcast: .78f, cirrus: 0f, thickness: .78f);

            case WeatherType.Rainy:
                return New(
                    top: new Color(.25f, .31f, .38f), bottom: new Color(.40f, .47f, .53f),
                    cloud: new Color(.43f, .49f, .56f), coverage: .82f,
                    fog: new Color(.40f, .47f, .53f), fogDensity: .0085f,
                    darkness: .50f, direct: .50f, ambient: .66f, wind: .64f,
                    overcast: .86f, cirrus: 0f, thickness: .86f);

            case WeatherType.Storm:
                return New(
                    top: new Color(.16f, .20f, .27f), bottom: new Color(.30f, .35f, .42f),
                    cloud: new Color(.27f, .32f, .39f), coverage: .93f,
                    fog: new Color(.28f, .34f, .40f), fogDensity: .0145f,
                    darkness: .76f, direct: .28f, ambient: .44f, wind: .98f,
                    overcast: .98f, cirrus: 0f, thickness: .98f);

            case WeatherType.Snowy:
                return New(
                    top: new Color(.69f, .77f, .85f), bottom: new Color(.86f, .91f, .95f),
                    cloud: new Color(.94f, .97f, 1f), coverage: .82f,
                    fog: new Color(.83f, .89f, .94f), fogDensity: .0105f,
                    darkness: .08f, direct: .76f, ambient: .96f, wind: .42f,
                    overcast: .82f, cirrus: 0f, thickness: .78f);

            case WeatherType.SnowStorm:
                return New(
                    top: new Color(.53f, .62f, .72f), bottom: new Color(.75f, .82f, .88f),
                    cloud: new Color(.83f, .89f, .95f), coverage: .94f,
                    fog: new Color(.75f, .82f, .89f), fogDensity: .029f,
                    darkness: .22f, direct: .48f, ambient: .80f, wind: 1f,
                    overcast: .98f, cirrus: 0f, thickness: .98f);

            case WeatherType.Foggy:
                return New(
                    top: new Color(.55f, .62f, .66f), bottom: new Color(.72f, .77f, .79f),
                    cloud: new Color(.78f, .81f, .83f), coverage: .48f,
                    fog: new Color(.72f, .76f, .78f), fogDensity: .028f,
                    darkness: .22f, direct: .58f, ambient: .82f, wind: .18f,
                    overcast: .36f, cirrus: 0f, thickness: .50f);

            default:
                return For(WeatherType.Sunny);
        }
    }

    private static WeatherVisuals New(
        Color top, Color bottom, Color cloud, float coverage,
        Color fog, float fogDensity, float darkness, float direct, float ambient,
        float wind, float overcast, float cirrus, float thickness)
    {
        return new WeatherVisuals
        {
            skyTopColor = top,
            skyBottomColor = bottom,
            cloudColor = cloud,
            cloudCoverage = coverage,
            fogColor = fog,
            fogDensity = fogDensity,
            darkness = darkness,
            directLightMultiplier = direct,
            ambientMultiplier = ambient,
            windStrength = wind,
            cloudOvercast = overcast,
            cirrusAmount = cirrus,
            cloudThickness = thickness
        };
    }
}
