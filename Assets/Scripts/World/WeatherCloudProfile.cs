using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Plots & Rots/Weather/3D Cloud Profile")]
public sealed class WeatherCloudProfile : ScriptableObject
{
    [Serializable]
    public struct Style
    {
        public WeatherType weather;
        public Vector2Int cloudCount;
        public Vector2 scaleRange;
        public Vector2 altitudeRange;
        public Vector2 opacityRange;
        public Vector2 transmissionRange;
        public Color tint;
        [Range(0f, 1f)] public float ceilingStrength;
        [Range(.25f, 3f)] public float windMultiplier;
    }

    [SerializeField] private Style[] styles = Array.Empty<Style>();

    public bool TryGet(WeatherType weather, out Style style)
    {
        if (styles != null)
        {
            foreach (Style candidate in styles)
            {
                if (candidate.weather != weather) continue;
                style = Sanitize(candidate);
                return true;
            }
        }

        style = Recommended(weather);
        return false;
    }

    [ContextMenu("Reset To Recommended PC Defaults")]
    private void ResetRecommended()
    {
        WeatherType[] values = (WeatherType[])Enum.GetValues(typeof(WeatherType));
        styles = new Style[values.Length];
        for (int i = 0; i < values.Length; i++)
            styles[i] = Recommended(values[i]);
    }

    public static Style Recommended(WeatherType weather)
    {
        switch (weather)
        {
            case WeatherType.Sunny:
                return New(weather, 3, 6, 10f, 18f, 170f, 330f, .84f, .98f, 0f, .04f,
                    new Color(1f, 1f, .99f), .02f, .70f);

            case WeatherType.PartlyCloudy:
                return New(weather, 7, 13, 12f, 24f, 155f, 315f, .86f, 1f, 0f, .03f,
                    new Color(.98f, .99f, 1f), .08f, .95f);

            case WeatherType.Cloudy:
                return New(weather, 8, 14, 15f, 28f, 140f, 285f, .90f, 1f, 0f, .02f,
                    new Color(.88f, .92f, .97f), .28f, 1.00f);

            case WeatherType.Overcast:
                return New(weather, 7, 12, 18f, 34f, 125f, 250f, .92f, 1f, 0f, .01f,
                    new Color(.76f, .81f, .88f), .82f, 1.10f);

            case WeatherType.Rainy:
                return New(weather, 6, 11, 24f, 42f, 105f, 225f, .95f, 1f, 0f, 0f,
                    new Color(.56f, .63f, .72f), .90f, 1.35f);

            case WeatherType.Storm:
                return New(weather, 8, 14, 30f, 52f, 90f, 205f, .97f, 1f, 0f, 0f,
                    new Color(.35f, .42f, .52f), 1f, 2.0f);

            case WeatherType.Snowy:
                return New(weather, 7, 12, 22f, 38f, 115f, 240f, .92f, 1f, 0f, 0f,
                    new Color(.92f, .96f, 1f), .84f, 1.00f);

            case WeatherType.SnowStorm:
                return New(weather, 8, 14, 26f, 46f, 95f, 215f, .96f, 1f, 0f, 0f,
                    new Color(.80f, .87f, .95f), .98f, 1.85f);

            case WeatherType.Foggy:
                return New(weather, 3, 6, 12f, 24f, 125f, 260f, .75f, .92f, 0f, .02f,
                    new Color(.87f, .89f, .92f), .35f, .55f);

            default:
                return Recommended(WeatherType.Sunny);
        }
    }

    private static Style New(
        WeatherType weather,
        int countMin,
        int countMax,
        float scaleMin,
        float scaleMax,
        float altitudeMin,
        float altitudeMax,
        float opacityMin,
        float opacityMax,
        float transmissionMin,
        float transmissionMax,
        Color tint,
        float ceiling,
        float wind)
    {
        return new Style
        {
            weather = weather,
            cloudCount = new Vector2Int(countMin, countMax),
            scaleRange = new Vector2(scaleMin, scaleMax),
            altitudeRange = new Vector2(altitudeMin, altitudeMax),
            opacityRange = new Vector2(opacityMin, opacityMax),
            transmissionRange = new Vector2(transmissionMin, transmissionMax),
            tint = tint,
            ceilingStrength = ceiling,
            windMultiplier = wind
        };
    }

    private static Style Sanitize(Style style)
    {
        style.cloudCount.x = Mathf.Max(0, style.cloudCount.x);
        style.cloudCount.y = Mathf.Max(style.cloudCount.x, style.cloudCount.y);
        style.scaleRange = Ordered(style.scaleRange, .1f);
        style.altitudeRange = Ordered(style.altitudeRange, 0f);
        style.opacityRange = ClampOrdered(style.opacityRange);
        style.transmissionRange = ClampOrdered(style.transmissionRange);
        style.ceilingStrength = Mathf.Clamp01(style.ceilingStrength);
        style.windMultiplier = Mathf.Max(.25f, style.windMultiplier);
        return style;
    }

    private static Vector2 Ordered(Vector2 value, float minimum)
    {
        float a = Mathf.Max(minimum, Mathf.Min(value.x, value.y));
        float b = Mathf.Max(a, Mathf.Max(value.x, value.y));
        return new Vector2(a, b);
    }

    private static Vector2 ClampOrdered(Vector2 value)
    {
        float a = Mathf.Clamp01(Mathf.Min(value.x, value.y));
        float b = Mathf.Clamp(Mathf.Max(value.x, value.y), a, 1f);
        return new Vector2(a, b);
    }
}