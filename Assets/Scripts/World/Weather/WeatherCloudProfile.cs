using System;
using UnityEngine;

namespace PlotNRots.World.Weather
{
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
                    return New(weather, 3, 6, 8f, 16f, 170f, 330f, .78f, .94f, .42f, .72f,
                        new Color(1f, 1f, .98f), .02f, .70f);
                case WeatherType.PartlyCloudy:
                    return New(weather, 8, 14, 10f, 23f, 150f, 315f, .82f, 1f, .30f, .64f,
                        new Color(.98f, .99f, 1f), .08f, .95f);
                case WeatherType.Cloudy:
                    return New(weather, 10, 16, 14f, 28f, 135f, 285f, .88f, 1f, .18f, .48f,
                        new Color(.88f, .92f, .96f), .30f, 1.05f);
                case WeatherType.Overcast:
                    return New(weather, 8, 13, 18f, 34f, 120f, 250f, .92f, 1f, .12f, .36f,
                        new Color(.76f, .81f, .87f), .82f, 1.15f);
                case WeatherType.Rainy:
                    return New(weather, 7, 12, 23f, 42f, 105f, 230f, .95f, 1f, .06f, .26f,
                        new Color(.55f, .61f, .68f), .90f, 1.35f);
                case WeatherType.Storm:
                    return New(weather, 10, 16, 28f, 50f, 90f, 210f, .98f, 1f, .02f, .14f,
                        new Color(.38f, .44f, .52f), 1f, 2.0f);
                case WeatherType.Snowy:
                    return New(weather, 8, 13, 20f, 38f, 115f, 245f, .90f, .99f, .18f, .46f,
                        new Color(.93f, .97f, 1f), .86f, 1.05f);
                case WeatherType.SnowStorm:
                    return New(weather, 10, 16, 25f, 46f, 95f, 220f, .96f, 1f, .06f, .24f,
                        new Color(.80f, .88f, .96f), .98f, 1.85f);
                case WeatherType.Foggy:
                    return New(weather, 3, 7, 12f, 25f, 125f, 260f, .72f, .90f, .35f, .62f,
                        new Color(.86f, .89f, .91f), .35f, .55f);
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
}
