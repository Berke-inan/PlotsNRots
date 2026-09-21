using UnityEngine;
using UnityEngine.Rendering;

// Owns one runtime sky material. No gameplay state, renderer scans, mesh clouds or per-object scripts.
[DisallowMultipleComponent, DefaultExecutionOrder(100)]
public class StylizedSkyController : MonoBehaviour
{
    [SerializeField] private SkyAtmosphereProfile profile;
    [SerializeField] private DayNightCycleManager clock;
    private Material originalSky, sky;
    private SkyAtmosphereProfile fallbackProfile;
    private WeatherVisuals weather;
    private float wind;
    private float lightningFlash;
    private Vector2 displacement;
    private AmbientMode previousAmbientMode;
    private Color previousSkyColor, previousEquatorColor, previousGroundColor, previousSunColor, previousMoonColor;
    private Light previousSun;
    public Material RuntimeMaterial => sky;

    private void OnEnable()
    {
        if (clock == null) clock = DayNightCycleManager.Instance;
        if (profile == null) { fallbackProfile = ScriptableObject.CreateInstance<SkyAtmosphereProfile>(); profile = fallbackProfile; }
        originalSky = RenderSettings.skybox;
        if (originalSky != null)
        {
            sky = new Material(originalSky) { name = originalSky.name + " (Runtime Atmosphere)" };
            RenderSettings.skybox = sky;
        }
        previousAmbientMode = RenderSettings.ambientMode;
        previousSkyColor = RenderSettings.ambientSkyColor;
        previousEquatorColor = RenderSettings.ambientEquatorColor;
        previousGroundColor = RenderSettings.ambientGroundColor;
        previousSun = RenderSettings.sun;
        if (clock != null && clock.sunLight != null) previousSunColor = clock.sunLight.color;
        if (clock != null && clock.moonLight != null) previousMoonColor = clock.moonLight.color;
    }
    public void SetWeather(WeatherVisuals state, float windStrength) { weather = state; wind = windStrength; }

    public void SetLightningFlash(float value)
    {
        lightningFlash = Mathf.Clamp01(value);
        if (isActiveAndEnabled) RenderNow();
    }

    public void ApplyWeather(WeatherVisuals state, float windStrength)
    {
        SetWeather(state, windStrength);
        RenderNow(); // Same path for load, enabling and ordinary interpolation.
    }
    private void LateUpdate()
    {
        if (profile == null) return;
        displacement += profile.WindDirection.normalized * (profile.CloudSpeed * Mathf.Lerp(.4f, 2, wind) * Time.deltaTime);
        RenderNow();
    }
    public void RenderNow()
    {
        if (profile == null) return;
        Vector3 sun = clock != null ? clock.SunDirection : Vector3.up;
        Vector3 moon = -sun;
        float daylight = SkyAtmosphereProfile.Daylight(sun.y);
        float twilight = SkyAtmosphereProfile.Twilight(sun.y);
        float darkness = Mathf.Clamp01(weather.darkness);
        Color zenith = Palette(profile.NightZenith, profile.DayZenith, profile.TwilightZenith, daylight, twilight, darkness);
        Color middle = Palette(profile.NightMiddle, profile.DayMiddle, profile.TwilightMiddle, daylight, twilight, darkness);
        Color horizon = Palette(profile.NightHorizon, profile.DayHorizon, profile.TwilightHorizon, daylight, twilight, darkness);
        Color litCloud = Color.Lerp(profile.NightCloudLight, profile.CloudLight, daylight);
        litCloud = Color.Lerp(litCloud, profile.TwilightHorizon, twilight * .65f) * Mathf.Lerp(1, .55f, darkness);
        Color cloudShade = Color.Lerp(profile.NightCloudShadow, profile.CloudShadow, daylight) * Mathf.Lerp(1, .55f, darkness);
        Color cloudBase = Color.Lerp(cloudShade, litCloud, Mathf.Lerp(.58f, .42f, weather.cloudThickness));
        Color cloudUnderside = Color.Lerp(cloudShade, profile.NightCloudShadow, profile.UndersideDarkness * Mathf.Lerp(.35f, 1, weather.cloudThickness));
        Color sunColor = Color.Lerp(profile.SunHorizon, profile.SunNoon, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .45f, sun.y)));

        // Closed weather must suppress the celestial layer even if the ceiling shader is visually
        // subtle. This prevents a Storm/Rain night from looking like a clear starry night.
        float ceilingOcclusion = Mathf.SmoothStep(.35f, .88f,
            Mathf.Clamp01(weather.cloudOvercast * Mathf.Lerp(.72f, 1f, weather.cloudThickness)));
        float starVisibility = SkyAtmosphereProfile.Stars(sun.y) * (1f - ceilingOcclusion);
        float moonVisibility = Mathf.Lerp(1f, .08f, ceilingOcclusion);
        float sunVisibility = Mathf.Lerp(1f, .38f, ceilingOcclusion);

        // Lightning is an atmosphere-wide event, not just a local ground light.
        Color lightningTint = new Color(.72f, .84f, 1f);
        float flash = Mathf.Clamp01(lightningFlash);
        zenith = Color.Lerp(zenith, lightningTint * .72f, flash * .58f);
        middle = Color.Lerp(middle, lightningTint * .86f, flash * .68f);
        horizon = Color.Lerp(horizon, lightningTint, flash * .74f);
        litCloud = Color.Lerp(litCloud, Color.white, flash * .92f);
        cloudBase = Color.Lerp(cloudBase, lightningTint, flash * .82f);
        cloudUnderside = Color.Lerp(cloudUnderside, lightningTint * .78f, flash * .76f);
        sunColor *= sunVisibility;

        Color fog = Color.Lerp(horizon, weather.fogColor * Mathf.Lerp(.16f, 1, daylight), .25f);
        fog = Color.Lerp(fog, lightningTint, flash * .55f);
        RenderSettings.fogColor = Opaque(fog);
        RenderSettings.fogDensity = weather.fogDensity * Mathf.Lerp(1.12f, 1, daylight);
        if (sky != null)
        {
            sky.SetColor("_SkyTopColor", Opaque(zenith)); sky.SetColor("_SkyMidColor", Opaque(middle)); sky.SetColor("_SkyBottomColor", Opaque(horizon));
            if (profile.CloudMask != null) sky.SetTexture("_CloudMaskTex", profile.CloudMask);
            sky.SetColor("_CloudHighlightColor", Opaque(litCloud * weather.cloudColor));
            sky.SetColor("_CloudBaseColor", Opaque(cloudBase * weather.cloudColor));
            sky.SetColor("_CloudUndersideColor", Opaque(cloudUnderside));
            sky.SetColor("_SunColor", sunColor);
            sky.SetColor("_MoonColor", profile.MoonTint * moonVisibility);
            sky.SetVector("_SunDir", sun); sky.SetVector("_MoonDir", moon);
            sky.SetFloat("_Daylight", daylight); sky.SetFloat("_Twilight", twilight);
            sky.SetFloat("_StarVisibility", starVisibility);
            sky.SetFloat("_StarDensity", profile.StarDensity); sky.SetFloat("_StarBrightness", profile.StarBrightness);
            sky.SetFloat("_SunAngularRadius", profile.SunAngularRadius); sky.SetFloat("_MoonAngularRadius", profile.MoonAngularRadius);
            sky.SetFloat("_SunHalo", profile.SunHalo); sky.SetFloat("_MoonHalo", profile.MoonHalo);
            sky.SetFloat("_CloudCoverage", weather.cloudCoverage); sky.SetFloat("_CloudScale", profile.MacroScale);
            sky.SetFloat("_CloudOvercast", weather.cloudOvercast); sky.SetFloat("_CirrusAmount", weather.cirrusAmount);
            sky.SetFloat("_CloudThickness", weather.cloudThickness); sky.SetFloat("_CloudLowerScale", profile.LowerLayerScale);
            sky.SetFloat("_CloudSoftness", profile.CoverageSoftness); sky.SetFloat("_CloudBreakup", profile.BreakupStrength);
            sky.SetFloat("_CloudToneSteps", profile.CloudToneSteps); sky.SetFloat("_CloudHighlightStrength", profile.HighlightStrength);
            sky.SetFloat("_CloudHorizonFade", profile.HorizonFade);
            sky.SetVector("_CloudMotion", new Vector4(displacement.x, displacement.y, profile.FarLayerSpeed, profile.NearLayerSpeed));
            float angle = clock != null ? clock.NormalizedTime * 360 : 0;
            sky.SetMatrix("_StarRotation", Matrix4x4.Rotate(Quaternion.Euler(25, angle, 12)));
        }
        // One global palette lights every pooled cloud renderer; cloud children have no scripts.
        Shader.SetGlobalVector("_StylizedCloudSunDirection", sun);
        Shader.SetGlobalColor("_StylizedCloudHighlight", Opaque(litCloud * weather.cloudColor));
        Shader.SetGlobalColor("_StylizedCloudBase", Opaque(cloudBase * weather.cloudColor));
        Shader.SetGlobalColor("_StylizedCloudUnderside", Opaque(cloudUnderside));
        if (clock != null)
        {
            float directMultiplier = Mathf.Lerp(weather.directLightMultiplier, 1f, flash * .35f);
            float ambientMultiplier = Mathf.Lerp(weather.ambientMultiplier, 1f, flash);
            Color ambientSky = Color.Lerp(Color.Lerp(profile.NightMiddle * 2, zenith, daylight), lightningTint, flash * .48f);
            Color ambientEquator = Color.Lerp(horizon * .8f, lightningTint * .82f, flash * .42f);
            Color ambientGround = Color.Lerp(horizon * .35f, lightningTint * .5f, flash * .30f);
            clock.ApplyAtmosphere(directMultiplier, ambientMultiplier, sunColor, profile.MoonTint * moonVisibility,
                Opaque(ambientSky), Opaque(ambientEquator), Opaque(ambientGround));
        }
    }
    private static Color Palette(Color night, Color day, Color twilightColor, float daylight, float twilight, float darkness)
    {
        Color color = Color.Lerp(Color.Lerp(night, day, daylight), twilightColor, twilight * (1 - darkness * .6f));
        float luminance = color.grayscale;
        return Color.Lerp(color, new Color(luminance * .72f, luminance * .8f, luminance * .9f), darkness * .65f) * Mathf.Lerp(1, .62f, darkness);
    }
    private static Color Opaque(Color color) { color.a = 1; return color; }
    private void OnDisable()
    {
        lightningFlash = 0f;
        if (RenderSettings.skybox == sky) RenderSettings.skybox = originalSky;
        if (sky != null) { if (Application.isPlaying) Destroy(sky); else DestroyImmediate(sky); }
        sky = null;
        if (clock != null)
        {
            clock.ClearAtmosphere();
            if (clock.sunLight != null) clock.sunLight.color = previousSunColor;
            if (clock.moonLight != null) clock.moonLight.color = previousMoonColor;
        }
        RenderSettings.ambientMode = previousAmbientMode;
        RenderSettings.ambientSkyColor = previousSkyColor; RenderSettings.ambientEquatorColor = previousEquatorColor; RenderSettings.ambientGroundColor = previousGroundColor;
        RenderSettings.sun = previousSun;
        if (fallbackProfile != null)
        {
            if (Application.isPlaying) Destroy(fallbackProfile); else DestroyImmediate(fallbackProfile);
            fallbackProfile = null; profile = null;
        }
    }
}
