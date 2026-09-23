using UnityEngine;

[System.Serializable]
public struct WeatherVisuals
{
    [Header("Sky")]
    public Color skyTopColor;
    public Color skyBottomColor;
    public Color cloudColor;
    [Range(0f, 1f)] public float cloudCoverage;

    [Header("Atmosphere")]
    [Min(0f)] public float fogDensity;
    public Color fogColor;
    [Range(0f, 1f)] public float darkness;
    [Range(0f, 1f)] public float directLightMultiplier;
    [Range(0f, 1f)] public float ambientMultiplier;
    [Range(0f, 1f)] public float windStrength;
    [Range(0f, 1f)] public float cloudOvercast;
    [Range(0f, 1f)] public float cirrusAmount;
    [Range(0f, 1f)] public float cloudThickness;

    public static WeatherVisuals Lerp(in WeatherVisuals a, in WeatherVisuals b, float t)
    {
        t = Mathf.Clamp01(t);
        return new WeatherVisuals
        {
            skyTopColor = Color.Lerp(a.skyTopColor, b.skyTopColor, t),
            skyBottomColor = Color.Lerp(a.skyBottomColor, b.skyBottomColor, t),
            cloudColor = Color.Lerp(a.cloudColor, b.cloudColor, t),
            cloudCoverage = Mathf.Lerp(a.cloudCoverage, b.cloudCoverage, t),
            fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, t),
            fogColor = Color.Lerp(a.fogColor, b.fogColor, t),
            darkness = Mathf.Lerp(a.darkness, b.darkness, t),
            directLightMultiplier = Mathf.Lerp(a.directLightMultiplier, b.directLightMultiplier, t),
            ambientMultiplier = Mathf.Lerp(a.ambientMultiplier, b.ambientMultiplier, t),
            windStrength = Mathf.Lerp(a.windStrength, b.windStrength, t),
            cloudOvercast = Mathf.Lerp(a.cloudOvercast, b.cloudOvercast, t),
            cirrusAmount = Mathf.Lerp(a.cirrusAmount, b.cirrusAmount, t),
            cloudThickness = Mathf.Lerp(a.cloudThickness, b.cloudThickness, t)
        };
    }
}
