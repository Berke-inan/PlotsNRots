using UnityEngine;
using UnityEngine.Rendering;

// Owns one runtime sky material. No gameplay state, renderer scans, mesh clouds or per-object scripts.
[DisallowMultipleComponent, DefaultExecutionOrder(100)]
public class StylizedSkyController : MonoBehaviour
{
    [SerializeField] private SkyAtmosphereProfile profile;
    [SerializeField] private DayNightCycleManager clock;

    [Header("Night Surface Visibility")]
    [Tooltip("Only lifts Trilight ambient colours used by terrain/objects. Sky colours are not changed.")]
    [SerializeField, Range(1f, 1.5f)] private float nightAmbientSkyLift = 1.10f;
    [SerializeField, Range(1f, 1.6f)] private float nightAmbientEquatorLift = 1.14f;
    [SerializeField, Range(1f, 1.8f)] private float nightAmbientGroundLift = 1.22f;

    [Header("Snowy Night - Warm Amber Sky v2")]
    [Tooltip("Warm orange/amber snowy-night sky strength. 0 = normal night palette, 1 = full authored amber palette.")]
    [SerializeField, Range(0f, 1f)] private float snowyNightAmberStrength = .82f;
    [SerializeField, Range(.8f, 1.35f)] private float snowyNightAmberBrightness = 1.06f;
    [SerializeField, Range(0f, 1f)] private float snowyNightAmberCloudWarmth = .62f;

    // v2 field names intentionally reset the old oversaturated crimson values already serialized
    // on the scene component. These three colours stay close together for a smooth amber dome.
    [SerializeField] private Color snowyNightAmberZenith = new Color(.48f, .29f, .27f, 1f);
    [SerializeField] private Color snowyNightAmberMiddle = new Color(.62f, .35f, .25f, 1f);
    [SerializeField] private Color snowyNightAmberHorizon = new Color(.74f, .43f, .27f, 1f);

    [Header("Snowy Night Environment Colour")]
    [Tooltip("Removes the red sky tint from terrain/objects while keeping the sky itself warm.")]
    [SerializeField, Range(0f, 1f)] private float snowyNightEnvironmentNeutralize = .94f;
    [SerializeField, Range(0f, 1f)] private float snowStormNightEnvironmentNeutralize = .82f;
    [SerializeField] private Color snowyNightAmbientSkyTint = new Color(.70f, .78f, .88f, 1f);
    [SerializeField] private Color snowyNightAmbientEquatorTint = new Color(.72f, .76f, .82f, 1f);
    [SerializeField] private Color snowyNightAmbientGroundTint = new Color(.60f, .66f, .74f, 1f);
    [SerializeField] private Color snowyNightFogTint = new Color(.58f, .62f, .69f, 1f);

    [Header("Snowy Night Surface Brightness")]
    [Tooltip("Only brightens terrain/objects during normal Snowy nights. The sky palette is not changed.")]
    [SerializeField, Range(1f, 1.8f)] private float snowyNightSurfaceBrightness = 1.22f;

    [Tooltip("Only brightens terrain/objects during SnowStorm nights. Kept subtle by default.")]
    [SerializeField, Range(1f, 1.5f)] private float snowStormNightSurfaceBrightness = 1.08f;

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

        // Snowy nights deliberately use a warm amber sky over the ENTIRE dome, not only the horizon.
        // Daytime snow remains untouched. At deep night the blend reaches the authored amber palette smoothly.
        WeatherType activeWeather = SeasonManager.Instance != null
            ? SeasonManager.Instance.currentWeather
            : WeatherType.Sunny;

        bool snowyWeather =
            activeWeather == WeatherType.Snowy
            || activeWeather == WeatherType.SnowStorm;

        float snowyNightBlend = snowyWeather
            ? Mathf.Clamp01((1f - daylight) * snowyNightAmberStrength)
            : 0f;

        if (snowyNightBlend > .001f)
        {
            // IMPORTANT: all three target colours stay in the same warm amber family.
            // LowPolySky.shader interpolates Bottom -> Middle -> Top by view height, so making
            // the zenith much darker than the horizon naturally created the old "red only on
            // the horizon" look. This palette intentionally keeps the zenith warmly coloured without becoming crimson.
            Color redZenith = snowyNightAmberZenith * snowyNightAmberBrightness;
            Color redMiddle = snowyNightAmberMiddle * snowyNightAmberBrightness;
            Color redHorizon = snowyNightAmberHorizon * snowyNightAmberBrightness;
            redZenith.a = redMiddle.a = redHorizon.a = 1f;

            zenith = Color.Lerp(zenith, redZenith, snowyNightBlend);
            middle = Color.Lerp(middle, redMiddle, snowyNightBlend);
            horizon = Color.Lerp(horizon, redHorizon, snowyNightBlend);

            // Snowy weather can render a continuous sky-ceiling above the horizon.
            // If that ceiling stays blue/dark, it visually replaces the red sky and creates
            // the hard red-horizon -> dark-center break seen in Game View.
            // Keep the snow ceiling in the same warm family as the dome itself.
            // A small minimum match is intentional so old serialized Inspector values cannot
            // bring the dark band back; the slider still controls the final strength.
            float cloudMatch = Mathf.Lerp(.86f, 1f, snowyNightAmberCloudWarmth);
            float cloudWarmBlend = snowyNightBlend * cloudMatch;

            Color redCloudHighlight = Color.Lerp(redMiddle, Color.white, .14f);
            Color redCloudBase = Color.Lerp(redZenith, redMiddle, .58f);
            Color redCloudUnderside = Color.Lerp(redZenith, redMiddle, .30f);

            litCloud = Color.Lerp(litCloud, redCloudHighlight, cloudWarmBlend);
            cloudBase = Color.Lerp(cloudBase, redCloudBase, cloudWarmBlend);
            cloudUnderside = Color.Lerp(cloudUnderside, redCloudUnderside, cloudWarmBlend);
        }

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
        if (snowyNightBlend > .001f)
        {
            // The sky may be warm, but real snowy ground/haze should stay mostly neutral/cool.
            // This prevents SnowStorm fog from washing the entire world red.
            float fogNeutralize = activeWeather == WeatherType.SnowStorm
                ? snowStormNightEnvironmentNeutralize
                : snowyNightEnvironmentNeutralize;

            fog = Color.Lerp(
                fog,
                MatchLuminance(fog, snowyNightFogTint),
                snowyNightBlend * fogNeutralize);
        }
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

            float nightVisibility =
                1f - daylight;

            // IMPORTANT: the warm snowy sky must NOT become the colour of the world lighting.
            // Keep snow, terrain, trees and buildings neutral/cool like a real illuminated winter night.
            if (snowyWeather && nightVisibility > .001f)
            {
                float environmentNeutralize = activeWeather == WeatherType.SnowStorm
                    ? snowStormNightEnvironmentNeutralize
                    : snowyNightEnvironmentNeutralize;

                float neutralBlend = nightVisibility * environmentNeutralize;

                ambientSky = Color.Lerp(
                    ambientSky,
                    MatchLuminance(ambientSky, snowyNightAmbientSkyTint),
                    neutralBlend);

                ambientEquator = Color.Lerp(
                    ambientEquator,
                    MatchLuminance(ambientEquator, snowyNightAmbientEquatorTint),
                    neutralBlend);

                ambientGround = Color.Lerp(
                    ambientGround,
                    MatchLuminance(ambientGround, snowyNightAmbientGroundTint),
                    neutralBlend);
            }

            ambientSky *=
                Mathf.Lerp(
                    1f,
                    nightAmbientSkyLift,
                    nightVisibility);

            ambientEquator *=
                Mathf.Lerp(
                    1f,
                    nightAmbientEquatorLift,
                    nightVisibility);

            ambientGround *=
                Mathf.Lerp(
                    1f,
                    nightAmbientGroundLift,
                    nightVisibility);

            // Extra surface visibility only for snowy nights. This does NOT change the sky
            // colours and does not affect Rainy/Storm/Sunny nights. The boost fades in
            // naturally as daylight disappears.
            float snowySurfaceBrightness = 1f;

            if (activeWeather == WeatherType.Snowy)
            {
                snowySurfaceBrightness =
                    Mathf.Lerp(
                        1f,
                        snowyNightSurfaceBrightness,
                        nightVisibility);
            }
            else if (activeWeather == WeatherType.SnowStorm)
            {
                snowySurfaceBrightness =
                    Mathf.Lerp(
                        1f,
                        snowStormNightSurfaceBrightness,
                        nightVisibility);
            }

            ambientSky *= snowySurfaceBrightness;
            ambientEquator *= snowySurfaceBrightness;
            ambientGround *= snowySurfaceBrightness;

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
    private static Color MatchLuminance(Color source, Color tint)
    {
        float sourceLuminance = Mathf.Max(.001f, source.grayscale);
        float tintLuminance = Mathf.Max(.001f, tint.grayscale);
        Color result = tint * (sourceLuminance / tintLuminance);
        result.a = 1f;
        return result;
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
