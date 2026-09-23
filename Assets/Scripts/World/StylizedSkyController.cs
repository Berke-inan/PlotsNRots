using UnityEngine;
using UnityEngine.Rendering;

// Existing scene-owned component intentionally remains in the global namespace
// so current Unity scene/prefab serialization is not broken.
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class StylizedSkyController : MonoBehaviour
{
    [SerializeField]
    private SkyAtmosphereProfile profile;

    [SerializeField]
    private DayNightCycleManager clock;

    private Material originalSky;
    private Material sky;

    private SkyAtmosphereProfile fallbackProfile;

    private WeatherVisuals weather;

    private float wind;
    private float lightningFlash;

    private Vector2 displacement;

    private AmbientMode previousAmbientMode;

    private Color previousSkyColor;
    private Color previousEquatorColor;
    private Color previousGroundColor;

    private Color previousSunColor;
    private Color previousMoonColor;

    private Light previousSun;

    private static readonly int CloudFogStrengthId =
        Shader.PropertyToID(
            "_StylizedCloudFogStrength");

    private static readonly int CloudFogNearId =
        Shader.PropertyToID(
            "_StylizedCloudFogNear");

    private static readonly int CloudFogFarId =
        Shader.PropertyToID(
            "_StylizedCloudFogFar");

    public Material RuntimeMaterial =>
        sky;


    // ============================================================
    // UNITY
    // ============================================================

    private void OnEnable()
    {
        if (clock == null)
        {
            clock =
                DayNightCycleManager.Instance;
        }

        if (profile == null)
        {
            fallbackProfile =
                ScriptableObject.CreateInstance<
                    SkyAtmosphereProfile>();

            profile =
                fallbackProfile;
        }

        originalSky =
            RenderSettings.skybox;

        if (originalSky != null)
        {
            sky =
                new Material(
                    originalSky)
                {
                    name =
                        originalSky.name
                        +
                        " (Runtime Atmosphere)"
                };

            RenderSettings.skybox =
                sky;
        }

        previousAmbientMode =
            RenderSettings.ambientMode;

        previousSkyColor =
            RenderSettings.ambientSkyColor;

        previousEquatorColor =
            RenderSettings.ambientEquatorColor;

        previousGroundColor =
            RenderSettings.ambientGroundColor;

        previousSun =
            RenderSettings.sun;

        if (clock != null
            &&
            clock.sunLight != null)
        {
            previousSunColor =
                clock.sunLight.color;
        }

        if (clock != null
            &&
            clock.moonLight != null)
        {
            previousMoonColor =
                clock.moonLight.color;
        }
    }


    private void LateUpdate()
    {
        if (profile == null)
            return;

        Vector2 direction =
            profile.WindDirection.sqrMagnitude
            >
            .0001f

                ?
                profile.WindDirection.normalized

                :
                Vector2.right;

        displacement +=
            direction
            *
            profile.CloudSpeed
            *
            Mathf.Lerp(
                .4f,
                2f,
                wind)
            *
            Time.deltaTime;

        RenderNow();
    }


    // ============================================================
    // PUBLIC
    // ============================================================

    public void SetWeather(
        WeatherVisuals state,
        float windStrength)
    {
        weather =
            state;

        wind =
            windStrength;
    }


    public void ApplyWeather(
        WeatherVisuals state,
        float windStrength)
    {
        SetWeather(
            state,
            windStrength);

        RenderNow();
    }


    public void SetLightningFlash(
        float value)
    {
        lightningFlash =
            Mathf.Clamp01(
                value);

        if (isActiveAndEnabled)
        {
            RenderNow();
        }
    }


    // ============================================================
    // RENDER
    // ============================================================

    public void RenderNow()
    {
        if (profile == null)
            return;


        WeatherType weatherType =
            SeasonManager.Instance != null

                ?
                SeasonManager.Instance.currentWeather

                :
                WeatherType.Sunny;


        Vector3 sun =
            clock != null

                ?
                clock.SunDirection

                :
                Vector3.up;


        Vector3 moon =
            -sun;


        float daylight =
            SkyAtmosphereProfile.Daylight(
                sun.y);


        float twilight =
            SkyAtmosphereProfile.Twilight(
                sun.y);


        float darkness =
            Mathf.Clamp01(
                weather.darkness);


        // ========================================================
        // BASE SKY
        // ========================================================

        Color zenith =
            Palette(
                profile.NightZenith,
                profile.DayZenith,
                profile.TwilightZenith,
                daylight,
                twilight,
                darkness);


        Color middle =
            Palette(
                profile.NightMiddle,
                profile.DayMiddle,
                profile.TwilightMiddle,
                daylight,
                twilight,
                darkness);


        Color horizon =
            Palette(
                profile.NightHorizon,
                profile.DayHorizon,
                profile.TwilightHorizon,
                daylight,
                twilight,
                darkness);


        // Weather-specific art direction.
        ApplyWeatherSkyPalette(
            weatherType,
            daylight,
            twilight,
            ref zenith,
            ref middle,
            ref horizon);


        // ========================================================
        // CLOUD PALETTE
        // ========================================================

        Color litCloud =
            Color.Lerp(
                profile.NightCloudLight,
                profile.CloudLight,
                daylight);


        Color cloudShade =
            Color.Lerp(
                profile.NightCloudShadow,
                profile.CloudShadow,
                daylight);


        Color cloudBase =
            Color.Lerp(
                cloudShade,
                litCloud,
                Mathf.Lerp(
                    .58f,
                    .42f,
                    weather.cloudThickness));


        Color cloudUnderside =
            Color.Lerp(
                cloudShade,
                profile.NightCloudShadow,
                profile.UndersideDarkness
                *
                Mathf.Lerp(
                    .35f,
                    1f,
                    weather.cloudThickness));


        ApplyWeatherCloudPalette(
            weatherType,
            daylight,
            twilight,
            ref litCloud,
            ref cloudBase,
            ref cloudUnderside);


        // ========================================================
        // SUN
        // ========================================================

        Color sunColor =
            Color.Lerp(
                profile.SunHorizon,
                profile.SunNoon,
                Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(
                        0f,
                        .45f,
                        sun.y)));


        ApplyWeatherSunPalette(
            weatherType,
            twilight,
            weather.cloudOvercast,
            ref sunColor);


        // ========================================================
        // CELESTIAL OCCLUSION
        // ========================================================

        float ceilingOcclusion =
            Mathf.SmoothStep(
                .32f,
                .86f,
                Mathf.Clamp01(
                    weather.cloudOvercast
                    *
                    Mathf.Lerp(
                        .74f,
                        1f,
                        weather.cloudThickness)));


        float starVisibility =
            SkyAtmosphereProfile.Stars(
                sun.y)
            *
            (1f - ceilingOcclusion);


        float moonVisibility =
            Mathf.Lerp(
                1f,
                .07f,
                ceilingOcclusion);


        float sunVisibility =
            Mathf.Lerp(
                1f,
                .32f,
                ceilingOcclusion);


        // ========================================================
        // LIGHTNING
        // ========================================================

        Color lightningTint =
            new Color(
                .72f,
                .84f,
                1f);


        float flash =
            Mathf.Clamp01(
                lightningFlash);


        zenith =
            Color.Lerp(
                zenith,
                lightningTint * .74f,
                flash * .60f);


        middle =
            Color.Lerp(
                middle,
                lightningTint * .88f,
                flash * .72f);


        horizon =
            Color.Lerp(
                horizon,
                lightningTint,
                flash * .78f);


        litCloud =
            Color.Lerp(
                litCloud,
                Color.white,
                flash * .88f);


        cloudBase =
            Color.Lerp(
                cloudBase,
                lightningTint,
                flash * .82f);


        cloudUnderside =
            Color.Lerp(
                cloudUnderside,
                lightningTint * .82f,
                flash * .76f);


        sunColor *=
            sunVisibility;


        // ========================================================
        // FOG
        // ========================================================

        Color fog =
            Color.Lerp(
                horizon,
                weather.fogColor,
                Mathf.Clamp01(
                    weather.fogDensity
                    *
                    24f));


        fog =
            Color.Lerp(
                fog,
                lightningTint,
                flash * .55f);


        RenderSettings.fogColor =
            Opaque(
                fog);


        RenderSettings.fogDensity =
            Mathf.Max(
                0f,
                weather.fogDensity)
            *
            Mathf.Lerp(
                1.08f,
                1f,
                daylight);


        Configure3DCloudFog(
            weatherType,
            weather.fogDensity);


        // ========================================================
        // SKY MATERIAL
        // ========================================================

        if (sky != null)
        {
            sky.SetColor(
                "_SkyTopColor",
                Opaque(
                    zenith));


            sky.SetColor(
                "_SkyMidColor",
                Opaque(
                    middle));


            sky.SetColor(
                "_SkyBottomColor",
                Opaque(
                    horizon));


            if (profile.CloudMask != null)
            {
                sky.SetTexture(
                    "_CloudMaskTex",
                    profile.CloudMask);
            }


            sky.SetColor(
                "_CloudHighlightColor",
                Opaque(
                    litCloud));


            sky.SetColor(
                "_CloudBaseColor",
                Opaque(
                    cloudBase));


            sky.SetColor(
                "_CloudUndersideColor",
                Opaque(
                    cloudUnderside));


            sky.SetColor(
                "_SunColor",
                Opaque(
                    sunColor));


            sky.SetColor(
                "_MoonColor",
                Opaque(
                    profile.MoonTint
                    *
                    moonVisibility));


            sky.SetVector(
                "_SunDir",
                sun);


            sky.SetVector(
                "_MoonDir",
                moon);


            sky.SetFloat(
                "_Daylight",
                daylight);


            sky.SetFloat(
                "_Twilight",
                twilight);


            sky.SetFloat(
                "_StarVisibility",
                starVisibility);


            sky.SetFloat(
                "_StarDensity",
                profile.StarDensity);


            sky.SetFloat(
                "_StarBrightness",
                profile.StarBrightness);


            sky.SetFloat(
                "_SunAngularRadius",
                profile.SunAngularRadius);


            sky.SetFloat(
                "_MoonAngularRadius",
                profile.MoonAngularRadius);


            sky.SetFloat(
                "_SunHalo",
                profile.SunHalo);


            sky.SetFloat(
                "_MoonHalo",
                profile.MoonHalo);


            sky.SetFloat(
                "_CloudCoverage",
                weather.cloudCoverage);


            sky.SetFloat(
                "_CloudScale",
                profile.MacroScale);


            sky.SetFloat(
                "_CloudOvercast",
                weather.cloudOvercast);


            sky.SetFloat(
                "_CirrusAmount",
                weather.cirrusAmount);


            sky.SetFloat(
                "_CloudThickness",
                weather.cloudThickness);


            sky.SetFloat(
                "_CloudLowerScale",
                profile.LowerLayerScale);


            sky.SetFloat(
                "_CloudSoftness",
                profile.CoverageSoftness);


            // Delikli yapý artýk hiçbir weather'da geri gelmesin.
            sky.SetFloat(
                "_CloudBreakup",
                0f);


            sky.SetFloat(
                "_CloudToneSteps",
                profile.CloudToneSteps);


            sky.SetFloat(
                "_CloudHighlightStrength",
                profile.HighlightStrength);


            sky.SetFloat(
                "_CloudHorizonFade",
                profile.HorizonFade);


            sky.SetVector(
                "_CloudMotion",
                new Vector4(
                    displacement.x,
                    displacement.y,
                    profile.FarLayerSpeed,
                    profile.NearLayerSpeed));


            float angle =
                clock != null

                    ?
                    clock.NormalizedTime
                    *
                    360f

                    :
                    0f;


            sky.SetMatrix(
                "_StarRotation",
                Matrix4x4.Rotate(
                    Quaternion.Euler(
                        25f,
                        angle,
                        12f)));
        }


        // ========================================================
        // 3D CLOUD GLOBAL PALETTE
        // ========================================================

        Shader.SetGlobalVector(
            "_StylizedCloudSunDirection",
            sun);


        Shader.SetGlobalColor(
            "_StylizedCloudHighlight",
            Opaque(
                litCloud));


        Shader.SetGlobalColor(
            "_StylizedCloudBase",
            Opaque(
                cloudBase));


        Shader.SetGlobalColor(
            "_StylizedCloudUnderside",
            Opaque(
                cloudUnderside));


        // ========================================================
        // WORLD AMBIENT / LIGHTING
        // ========================================================

        if (clock != null)
        {
            float nightAmbient =
                NightAmbientFloor(
                    weatherType);


            float timeAmbientFactor =
                Mathf.Lerp(
                    nightAmbient,
                    1f,
                    daylight);


            float directMultiplier =
                Mathf.Lerp(
                    weather.directLightMultiplier,
                    1f,
                    flash * .35f);


            float ambientMultiplier =
                Mathf.Lerp(
                    weather.ambientMultiplier
                    *
                    timeAmbientFactor,
                    1f,
                    flash);


            Color ambientSky =
                Color.Lerp(
                    middle
                    *
                    Mathf.Lerp(
                        .68f,
                        .92f,
                        daylight),
                    lightningTint,
                    flash * .50f);


            Color ambientEquator =
                Color.Lerp(
                    horizon
                    *
                    Mathf.Lerp(
                        .58f,
                        .78f,
                        daylight),
                    lightningTint
                    *
                    .84f,
                    flash * .44f);


            Color ambientGround =
                Color.Lerp(
                    horizon
                    *
                    Mathf.Lerp(
                        .22f,
                        .34f,
                        daylight),
                    lightningTint
                    *
                    .52f,
                    flash * .32f);


            clock.ApplyAtmosphere(
                directMultiplier,
                ambientMultiplier,
                Opaque(
                    sunColor),
                Opaque(
                    profile.MoonTint
                    *
                    moonVisibility),
                Opaque(
                    ambientSky),
                Opaque(
                    ambientEquator),
                Opaque(
                    ambientGround));
        }
    }


    // ============================================================
    // WEATHER SKY PALETTE
    // ============================================================

    private static void ApplyWeatherSkyPalette(
        WeatherType weatherType,
        float daylight,
        float twilight,
        ref Color zenith,
        ref Color middle,
        ref Color horizon)
    {
        Color top;
        Color mid;
        Color bottom;

        float strength;


        switch (weatherType)
        {
            // ----------------------------------------------------
            // ROMANTIC BLUE / VIOLET RAIN
            // ----------------------------------------------------

            case WeatherType.Rainy:

                top =
                    TimeColor(
                        daylight,
                        twilight,

                        // Night
                        new Color(
                            .055f,
                            .065f,
                            .135f),

                        // Day
                        new Color(
                            .32f,
                            .38f,
                            .61f),

                        // Twilight
                        new Color(
                            .27f,
                            .25f,
                            .53f));


                mid =
                    TimeColor(
                        daylight,
                        twilight,

                        new Color(
                            .095f,
                            .105f,
                            .20f),

                        new Color(
                            .43f,
                            .48f,
                            .70f),

                        new Color(
                            .39f,
                            .33f,
                            .63f));


                bottom =
                    TimeColor(
                        daylight,
                        twilight,

                        new Color(
                            .15f,
                            .16f,
                            .27f),

                        new Color(
                            .61f,
                            .64f,
                            .79f),

                        new Color(
                            .54f,
                            .45f,
                            .70f));


                strength =
                    .92f;

                break;


            // ----------------------------------------------------
            // HEAVIER BLUE STORM
            // ----------------------------------------------------

            case WeatherType.Storm:

                top =
                    TimeColor(
                        daylight,
                        twilight,

                        new Color(
                            .035f,
                            .045f,
                            .095f),

                        new Color(
                            .13f,
                            .18f,
                            .34f),

                        new Color(
                            .085f,
                            .085f,
                            .23f));


                mid =
                    TimeColor(
                        daylight,
                        twilight,

                        new Color(
                            .070f,
                            .085f,
                            .15f),

                        new Color(
                            .22f,
                            .29f,
                            .44f),

                        new Color(
                            .14f,
                            .13f,
                            .31f));


                bottom =
                    TimeColor(
                        daylight,
                        twilight,

                        new Color(
                            .12f,
                            .14f,
                            .21f),

                        new Color(
                            .36f,
                            .43f,
                            .55f),

                        new Color(
                            .24f,
                            .21f,
                            .39f));


                strength =
                    .93f;

                break;


            // ----------------------------------------------------
            // NORMAL SNOW:
            // COLD DAY + WARM AMBER / RED WINTER NIGHT
            // ----------------------------------------------------

            case WeatherType.Snowy:

                top =
                    TimeColor(
                        daylight,
                        twilight,

                        // Warm dark winter night.
                        new Color(
                            .10f,
                            .065f,
                            .065f),

                        // Cold daylight.
                        new Color(
                            .53f,
                            .67f,
                            .80f),

                        // Sunset.
                        new Color(
                            .31f,
                            .16f,
                            .13f));


                mid =
                    TimeColor(
                        daylight,
                        twilight,

                        new Color(
                            .19f,
                            .105f,
                            .085f),

                        new Color(
                            .70f,
                            .79f,
                            .87f),

                        new Color(
                            .49f,
                            .26f,
                            .16f));


                bottom =
                    TimeColor(
                        daylight,
                        twilight,

                        new Color(
                            .39f,
                            .20f,
                            .105f),

                        new Color(
                            .86f,
                            .90f,
                            .94f),

                        new Color(
                            .72f,
                            .40f,
                            .18f));


                strength =
                    .91f;

                break;


            // ----------------------------------------------------
            // SNOW STORM - still readable at night
            // ----------------------------------------------------

            case WeatherType.SnowStorm:

                top =
                    TimeColor(
                        daylight,
                        twilight,

                        new Color(
                            .055f,
                            .055f,
                            .090f),

                        new Color(
                            .38f,
                            .49f,
                            .63f),

                        new Color(
                            .15f,
                            .10f,
                            .14f));


                mid =
                    TimeColor(
                        daylight,
                        twilight,

                        new Color(
                            .10f,
                            .09f,
                            .12f),

                        new Color(
                            .56f,
                            .66f,
                            .76f),

                        new Color(
                            .27f,
                            .17f,
                            .17f));


                bottom =
                    TimeColor(
                        daylight,
                        twilight,

                        new Color(
                            .23f,
                            .15f,
                            .11f),

                        new Color(
                            .72f,
                            .79f,
                            .85f),

                        new Color(
                            .48f,
                            .29f,
                            .18f));


                strength =
                    .92f;

                break;


            case WeatherType.Overcast:

                top =
                    Color.Lerp(
                        new Color(
                            .055f,
                            .065f,
                            .095f),
                        new Color(
                            .34f,
                            .43f,
                            .55f),
                        daylight);


                mid =
                    Color.Lerp(
                        new Color(
                            .085f,
                            .095f,
                            .13f),
                        new Color(
                            .46f,
                            .55f,
                            .65f),
                        daylight);


                bottom =
                    Color.Lerp(
                        new Color(
                            .13f,
                            .14f,
                            .18f),
                        new Color(
                            .61f,
                            .68f,
                            .74f),
                        daylight);


                strength =
                    .64f;

                break;


            case WeatherType.Foggy:

                top =
                    Color.Lerp(
                        new Color(
                            .06f,
                            .065f,
                            .08f),
                        new Color(
                            .46f,
                            .53f,
                            .59f),
                        daylight);


                mid =
                    Color.Lerp(
                        new Color(
                            .085f,
                            .09f,
                            .105f),
                        new Color(
                            .58f,
                            .64f,
                            .68f),
                        daylight);


                bottom =
                    Color.Lerp(
                        new Color(
                            .12f,
                            .12f,
                            .13f),
                        new Color(
                            .70f,
                            .73f,
                            .75f),
                        daylight);


                strength =
                    .72f;

                break;


            default:

                return;
        }


        zenith =
            Color.Lerp(
                zenith,
                top,
                strength);


        middle =
            Color.Lerp(
                middle,
                mid,
                strength);


        horizon =
            Color.Lerp(
                horizon,
                bottom,
                strength);
    }


    // ============================================================
    // WEATHER CLOUD PALETTE
    // ============================================================

    private static void ApplyWeatherCloudPalette(
        WeatherType weatherType,
        float daylight,
        float twilight,
        ref Color highlight,
        ref Color baseColor,
        ref Color underside)
    {
        Color h;
        Color b;
        Color u;

        float strength;


        switch (weatherType)
        {
            case WeatherType.Rainy:

                // Lavender-blue rain clouds.
                h =
                    TimeColor(
                        daylight,
                        twilight,
                        new Color(
                            .20f,
                            .20f,
                            .34f),
                        new Color(
                            .72f,
                            .74f,
                            .88f),
                        new Color(
                            .54f,
                            .46f,
                            .76f));


                b =
                    TimeColor(
                        daylight,
                        twilight,
                        new Color(
                            .12f,
                            .13f,
                            .24f),
                        new Color(
                            .52f,
                            .56f,
                            .73f),
                        new Color(
                            .39f,
                            .34f,
                            .59f));


                u =
                    TimeColor(
                        daylight,
                        twilight,
                        new Color(
                            .075f,
                            .085f,
                            .16f),
                        new Color(
                            .34f,
                            .38f,
                            .54f),
                        new Color(
                            .27f,
                            .24f,
                            .43f));


                strength =
                    .94f;

                break;


            case WeatherType.Storm:

                h =
                    Color.Lerp(
                        new Color(
                            .16f,
                            .18f,
                            .29f),
                        new Color(
                            .49f,
                            .54f,
                            .70f),
                        daylight);


                b =
                    Color.Lerp(
                        new Color(
                            .095f,
                            .11f,
                            .20f),
                        new Color(
                            .31f,
                            .36f,
                            .51f),
                        daylight);


                u =
                    Color.Lerp(
                        new Color(
                            .055f,
                            .065f,
                            .12f),
                        new Color(
                            .18f,
                            .21f,
                            .32f),
                        daylight);


                strength =
                    .95f;

                break;


            case WeatherType.Snowy:

                h =
                    TimeColor(
                        daylight,
                        twilight,
                        new Color(
                            .46f,
                            .31f,
                            .25f),
                        new Color(
                            .91f,
                            .94f,
                            .98f),
                        new Color(
                            .74f,
                            .45f,
                            .29f));


                b =
                    TimeColor(
                        daylight,
                        twilight,
                        new Color(
                            .29f,
                            .20f,
                            .18f),
                        new Color(
                            .72f,
                            .79f,
                            .86f),
                        new Color(
                            .52f,
                            .31f,
                            .23f));


                u =
                    TimeColor(
                        daylight,
                        twilight,
                        new Color(
                            .17f,
                            .12f,
                            .12f),
                        new Color(
                            .52f,
                            .59f,
                            .68f),
                        new Color(
                            .34f,
                            .22f,
                            .19f));


                strength =
                    .91f;

                break;


            case WeatherType.SnowStorm:

                h =
                    Color.Lerp(
                        new Color(
                            .25f,
                            .20f,
                            .20f),
                        new Color(
                            .80f,
                            .86f,
                            .92f),
                        daylight);


                b =
                    Color.Lerp(
                        new Color(
                            .16f,
                            .14f,
                            .16f),
                        new Color(
                            .59f,
                            .67f,
                            .76f),
                        daylight);


                u =
                    Color.Lerp(
                        new Color(
                            .10f,
                            .095f,
                            .12f),
                        new Color(
                            .43f,
                            .50f,
                            .59f),
                        daylight);


                strength =
                    .92f;

                break;


            case WeatherType.Overcast:

                h =
                    Color.Lerp(
                        new Color(
                            .18f,
                            .19f,
                            .24f),
                        new Color(
                            .70f,
                            .75f,
                            .81f),
                        daylight);


                b =
                    Color.Lerp(
                        new Color(
                            .12f,
                            .13f,
                            .17f),
                        new Color(
                            .56f,
                            .61f,
                            .67f),
                        daylight);


                u =
                    Color.Lerp(
                        new Color(
                            .075f,
                            .085f,
                            .11f),
                        new Color(
                            .38f,
                            .43f,
                            .49f),
                        daylight);


                strength =
                    .72f;

                break;


            default:

                return;
        }


        highlight =
            Color.Lerp(
                highlight,
                h,
                strength);


        baseColor =
            Color.Lerp(
                baseColor,
                b,
                strength);


        underside =
            Color.Lerp(
                underside,
                u,
                strength);
    }


    // ============================================================
    // SUN PALETTE
    // ============================================================

    private static void ApplyWeatherSunPalette(
        WeatherType weatherType,
        float twilight,
        float overcast,
        ref Color sunColor)
    {
        if (weatherType
                !=
                WeatherType.Rainy
            &&
            weatherType
                !=
                WeatherType.Storm
            &&
            weatherType
                !=
                WeatherType.Overcast)
        {
            return;
        }


        Color coolSun =
            weatherType
                ==
                WeatherType.Storm

                ?
                new Color(
                    .56f,
                    .67f,
                    .84f)

                :
                new Color(
                    .72f,
                    .74f,
                    .94f);


        float amount =
            Mathf.Clamp01(
                overcast
                *
                .74f
                +
                twilight
                *
                .35f);


        sunColor =
            Color.Lerp(
                sunColor,
                coolSun,
                amount);


        sunColor *=
            Mathf.Lerp(
                1f,
                .66f,
                overcast);
    }


    // ============================================================
    // NIGHT BRIGHTNESS
    // ============================================================

    private static float NightAmbientFloor(
        WeatherType weatherType)
    {
        switch (weatherType)
        {
            // Storms were previously much too dark.
            case WeatherType.Storm:
                return .44f;

            case WeatherType.SnowStorm:
                return .47f;

            case WeatherType.Rainy:
                return .48f;

            case WeatherType.Snowy:
                return .52f;

            case WeatherType.Overcast:
                return .45f;

            case WeatherType.Foggy:
                return .46f;

            default:
                return .40f;
        }
    }


    // ============================================================
    // 3D CLOUD FOG
    // ============================================================

    private static void Configure3DCloudFog(
        WeatherType weatherType,
        float fogDensity)
    {
        float strength =
            0f;

        float near =
            200f;

        float far =
            850f;


        switch (weatherType)
        {
            case WeatherType.Storm:

                strength =
                    Mathf.Clamp01(
                        Mathf.InverseLerp(
                            .005f,
                            .016f,
                            fogDensity));

                near =
                    180f;

                far =
                    720f;

                break;


            case WeatherType.SnowStorm:

                strength =
                    Mathf.Clamp01(
                        Mathf.InverseLerp(
                            .008f,
                            .026f,
                            fogDensity));

                near =
                    110f;

                far =
                    560f;

                break;


            case WeatherType.Foggy:

                strength =
                    Mathf.Clamp01(
                        Mathf.InverseLerp(
                            .008f,
                            .030f,
                            fogDensity));

                near =
                    80f;

                far =
                    420f;

                break;
        }


        Shader.SetGlobalFloat(
            CloudFogStrengthId,
            strength);


        Shader.SetGlobalFloat(
            CloudFogNearId,
            near);


        Shader.SetGlobalFloat(
            CloudFogFarId,
            far);
    }


    // ============================================================
    // HELPERS
    // ============================================================

    private static Color TimeColor(
        float daylight,
        float twilight,
        Color night,
        Color day,
        Color twilightColor)
    {
        Color value =
            Color.Lerp(
                night,
                day,
                daylight);


        return
            Color.Lerp(
                value,
                twilightColor,
                twilight);
    }


    private static Color Palette(
        Color night,
        Color day,
        Color twilightColor,
        float daylight,
        float twilight,
        float darkness)
    {
        Color color =
            Color.Lerp(
                Color.Lerp(
                    night,
                    day,
                    daylight),
                twilightColor,
                twilight
                *
                (
                    1f
                    -
                    darkness
                    *
                    .60f
                ));


        float luminance =
            color.grayscale;


        Color desaturated =
            new Color(
                luminance * .72f,
                luminance * .80f,
                luminance * .90f);


        return
            Color.Lerp(
                color,
                desaturated,
                darkness * .60f)
            *
            Mathf.Lerp(
                1f,
                .68f,
                darkness);
    }


    private static Color Opaque(
        Color color)
    {
        color.a =
            1f;

        return
            color;
    }


    // ============================================================
    // CLEANUP
    // ============================================================

    private void OnDisable()
    {
        lightningFlash =
            0f;


        Shader.SetGlobalFloat(
            CloudFogStrengthId,
            0f);


        if (RenderSettings.skybox == sky)
        {
            RenderSettings.skybox =
                originalSky;
        }


        if (sky != null)
        {
            if (Application.isPlaying)
            {
                Destroy(
                    sky);
            }
            else
            {
                DestroyImmediate(
                    sky);
            }
        }


        sky =
            null;


        if (clock != null)
        {
            clock.ClearAtmosphere();


            if (clock.sunLight != null)
            {
                clock.sunLight.color =
                    previousSunColor;
            }


            if (clock.moonLight != null)
            {
                clock.moonLight.color =
                    previousMoonColor;
            }
        }


        RenderSettings.ambientMode =
            previousAmbientMode;


        RenderSettings.ambientSkyColor =
            previousSkyColor;


        RenderSettings.ambientEquatorColor =
            previousEquatorColor;


        RenderSettings.ambientGroundColor =
            previousGroundColor;


        RenderSettings.sun =
            previousSun;


        if (fallbackProfile != null)
        {
            if (Application.isPlaying)
            {
                Destroy(
                    fallbackProfile);
            }
            else
            {
                DestroyImmediate(
                    fallbackProfile);
            }

            fallbackProfile =
                null;

            profile =
                null;
        }
    }
}