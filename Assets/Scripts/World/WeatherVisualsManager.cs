using System.Collections;
using PlotNRots.SaveSystem;
using PlotNRots.World.Weather;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeatherVisualsManager : MonoBehaviour
{
    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("Core References")]

    [SerializeField]
    private DayNightCycleManager clock;

    [SerializeField]
    private StylizedSkyController skyController;

    [SerializeField]
    private StylizedCloudManager cloudManager;

    [SerializeField]
    private GpuPrecipitationSystem precipitationSystem;

    [SerializeField]
    private WeatherExposureController exposureController;

    [SerializeField]
    private WeatherAppearanceProfile appearanceProfile;


    // ============================================================
    // AUDIO
    // ============================================================

    [Header("Weather Audio")]

    [SerializeField]
    private AudioClip rainLoop;

    [SerializeField]
    private AudioClip windLoop;

    [SerializeField]
    [Range(0f, 1f)]
    private float maxRainVolume = .78f;

    [SerializeField]
    [Range(0f, 1f)]
    private float maxWindVolume = .58f;

    [SerializeField]
    [Range(0f, 1f)]
    private float indoorRainVolume = .22f;

    [SerializeField]
    [Range(0f, 1f)]
    private float indoorWindVolume = .28f;

    [SerializeField]
    [Range(0f, 1f)]
    private float indoorThunderVolume = .38f;

    [SerializeField]
    [Range(500f, 22000f)]
    private float indoorLowPassHz = 1800f;


    // ============================================================
    // LIGHTNING
    // ============================================================

    [Header("Lightning / Thunder - Storm Only")]

    [SerializeField]
    private Light lightningLight;

    [SerializeField]
    private AudioClip[] thunderClips;

    [SerializeField]
    [Range(0f, 1f)]
    private float maxThunderVolume = .95f;

    [SerializeField]
    private Vector2 lightningInterval =
        new Vector2(
            9f,
            25f);

    [SerializeField]
    private Vector2 thunderDelay =
        new Vector2(
            .8f,
            2.6f);

    [SerializeField]
    [Min(0f)]
    private float lightningIntensity = 2.25f;

    [SerializeField]
    [Range(0f, 1f)]
    private float skyLightningFlashStrength = .95f;

    [SerializeField]
    [Range(0f, 1f)]
    private float stormStartRainThreshold = .38f;


    // ============================================================
    // TRANSITION
    // ============================================================

    [Header("Weather Transition")]

    [SerializeField]
    [Min(0f)]
    private float transitionDuration = 14f;

    [SerializeField]
    [Range(0f, .9f)]
    private float precipitationDelayFraction = .18f;


    // ============================================================
    // WIND
    // ============================================================

    [Header("World Wind")]

    [SerializeField]
    private Vector2 windDirection =
        new Vector2(
            1f,
            .3f);

    [SerializeField]
    [Min(0f)]
    private float maximumPrecipitationDrift = 8f;

    [SerializeField]
    private WindZone windZone;

    [SerializeField]
    [Min(0f)]
    private float maximumWindZoneStrength = 2f;


    // ============================================================
    // STATE
    // ============================================================

    private WeatherVisuals currentVisuals;
    private WeatherVisuals fromVisuals;
    private WeatherVisuals targetVisuals;

    private WeatherType targetWeather =
        WeatherType.Sunny;

    private float targetWeatherIntensity;

    private float rain;
    private float snow;
    private float wind;

    private float fromRain;
    private float fromSnow;
    private float fromWind;

    private float targetRain;
    private float targetSnow;
    private float targetWind;

    private float elapsed;

    private bool transitioning;
    private bool started;

    private Coroutine stormCoroutine;


    // ============================================================
    // AUDIO RUNTIME
    // ============================================================

    private AudioSource rainSource;
    private AudioSource windSource;
    private AudioSource thunderSource;

    private AudioLowPassFilter rainLowPass;
    private AudioLowPassFilter windLowPass;
    private AudioLowPassFilter thunderLowPass;


    // ============================================================
    // FOG BACKUP
    // ============================================================

    private bool originalFog;
    private FogMode originalFogMode;
    private Color originalFogColor;
    private float originalFogDensity;


    // ============================================================
    // PUBLIC
    // ============================================================

    public WeatherVisuals CurrentVisuals =>
        currentVisuals;

    public float CurrentRainAmount =>
        rain;

    public float CurrentSnowAmount =>
        snow;

    public float CurrentWindAmount =>
        wind;


    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        if (clock == null)
        {
            clock =
                DayNightCycleManager.Instance;
        }

        if (skyController == null)
        {
            skyController =
                GetComponent<
                    StylizedSkyController>();
        }

        if (cloudManager == null)
        {
            cloudManager =
                GetComponent<
                    StylizedCloudManager>();
        }

        if (precipitationSystem == null)
        {
            precipitationSystem =
                GetComponent<
                    GpuPrecipitationSystem>();
        }

        if (exposureController == null)
        {
            exposureController =
                GetComponent<
                    WeatherExposureController>();
        }


        rainSource =
            CreateLoopSource(
                "Weather Rain Audio",
                rainLoop,
                out rainLowPass);


        windSource =
            CreateLoopSource(
                "Weather Wind Audio",
                windLoop,
                out windLowPass);


        thunderSource =
            CreateOneShotSource(
                "Weather Thunder Audio",
                out thunderLowPass);


        if (lightningLight != null)
        {
            lightningLight.intensity =
                0f;
        }
    }


    private void OnEnable()
    {
        SeasonManager.OnWeatherChanged +=
            HandleWeatherChanged;


        SaveManager.OnGameLoaded +=
            ApplyWeatherImmediate;


        originalFog =
            RenderSettings.fog;


        originalFogMode =
            RenderSettings.fogMode;


        originalFogColor =
            RenderSettings.fogColor;


        originalFogDensity =
            RenderSettings.fogDensity;


        RenderSettings.fog =
            true;


        RenderSettings.fogMode =
            FogMode.ExponentialSquared;


        if (started)
        {
            ApplyWeatherImmediate();
        }
    }


    private void Start()
    {
        started =
            true;


        if (clock == null)
        {
            clock =
                DayNightCycleManager.Instance;
        }


        ApplyWeatherImmediate();
    }


    private void OnDisable()
    {
        SeasonManager.OnWeatherChanged -=
            HandleWeatherChanged;


        SaveManager.OnGameLoaded -=
            ApplyWeatherImmediate;


        StopThunderstorm();


        StopSource(
            rainSource);


        StopSource(
            windSource);


        if (precipitationSystem != null)
        {
            precipitationSystem.SetState(
                WeatherType.Sunny,
                0f,
                0f,
                Vector3.zero);
        }


        if (windZone != null)
        {
            windZone.windMain =
                0f;
        }


        RenderSettings.fog =
            originalFog;


        RenderSettings.fogMode =
            originalFogMode;


        RenderSettings.fogColor =
            originalFogColor;


        RenderSettings.fogDensity =
            originalFogDensity;
    }


    private void LateUpdate()
    {
        if (transitioning)
        {
            elapsed +=
                Time.deltaTime;


            float t =
                Mathf.Clamp01(
                    elapsed
                    /
                    Mathf.Max(
                        .001f,
                        transitionDuration));


            float atmosphereT =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t);


            float precipitationT =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(
                        precipitationDelayFraction,
                        1f,
                        t));


            currentVisuals =
                Blend(
                    fromVisuals,
                    targetVisuals,
                    atmosphereT);


            rain =
                Mathf.Lerp(
                    fromRain,
                    targetRain,
                    targetRain > fromRain

                        ?
                        precipitationT

                        :
                        atmosphereT);


            snow =
                Mathf.Lerp(
                    fromSnow,
                    targetSnow,
                    targetSnow > fromSnow

                        ?
                        precipitationT

                        :
                        atmosphereT);


            wind =
                Mathf.Lerp(
                    fromWind,
                    targetWind,
                    precipitationT);


            if (t >= 1f)
            {
                transitioning =
                    false;
            }
        }


        ApplyVisuals(
            false);


        UpdateThunderstormState();
    }


    // ============================================================
    // WEATHER STATE
    // ============================================================

    private void HandleWeatherChanged(
        WeatherType _)
    {
        if (!started)
            return;


        if (SeasonManager.Instance != null
            &&
            SeasonManager.Instance.IsRestoringState)
        {
            ApplyWeatherImmediate();
        }
        else
        {
            TransitionToWeather();
        }
    }


    public void TransitionToWeather()
    {
        StopThunderstorm();

        ReadTarget();


        fromVisuals =
            currentVisuals;


        fromRain =
            rain;


        fromSnow =
            snow;


        fromWind =
            wind;


        elapsed =
            0f;


        transitioning =
            true;


        if (transitionDuration <= 0f)
        {
            ApplyWeatherImmediate();
        }
    }


    public void ApplyWeatherImmediate()
    {
        if (!isActiveAndEnabled)
            return;


        StopThunderstorm();

        ReadTarget();


        currentVisuals =
            targetVisuals;


        rain =
            targetRain;


        snow =
            targetSnow;


        wind =
            targetWind;


        transitioning =
            false;


        ApplyVisuals(
            true);


        UpdateThunderstormState();
    }


    private void ReadTarget()
    {
        SeasonManager manager =
            SeasonManager.Instance;


        targetWeather =
            manager != null

                ?
                manager.currentWeather

                :
                WeatherType.Sunny;


        targetWeatherIntensity =
            manager != null

                ?
                manager.WeatherIntensity

                :
                0f;


        if (appearanceProfile != null
            &&
            appearanceProfile.TryGet(
                targetWeather,
                out WeatherVisuals appearance))
        {
            targetVisuals =
                appearance;
        }
        else
        {
            targetVisuals =
                DefaultVisualsFor(
                    targetWeather);
        }


        GradeForWeather(
            ref targetVisuals,
            targetWeather,
            targetWeatherIntensity);


        targetRain =
            WeatherRules.IsRain(
                targetWeather)

                ?
                targetWeatherIntensity

                :
                0f;


        targetSnow =
            WeatherRules.IsSnow(
                targetWeather)

                ?
                targetWeatherIntensity

                :
                0f;


        targetWind =
            targetVisuals.windStrength
            *
            Mathf.Lerp(
                .55f,
                1f,
                targetWeatherIntensity);
    }


    // ============================================================
    // APPLY
    // ============================================================

    private void ApplyVisuals(
        bool immediate)
    {
        if (skyController != null
            &&
            skyController.isActiveAndEnabled)
        {
            if (immediate)
            {
                skyController.ApplyWeather(
                    currentVisuals,
                    wind);
            }
            else
            {
                skyController.SetWeather(
                    currentVisuals,
                    wind);
            }
        }


        if (cloudManager != null
            &&
            cloudManager.isActiveAndEnabled)
        {
            if (immediate)
            {
                cloudManager.ApplyWeather(
                    targetWeather,
                    targetWeatherIntensity,
                    currentVisuals,
                    wind);
            }
            else
            {
                cloudManager.SetWeather(
                    targetWeather,
                    targetWeatherIntensity,
                    currentVisuals,
                    wind);
            }
        }


        float audioExposure =
            exposureController != null

                ?
                exposureController.AudioExposure

                :
                1f;


        // New world-collision precipitation system:
        // precipitation itself is never globally disabled indoors.
        float precipitationExposure =
            exposureController != null

                ?
                exposureController.PrecipitationExposure

                :
                1f;


        Vector2 direction2 =
            windDirection.sqrMagnitude
            >
            .0001f

                ?
                windDirection.normalized

                :
                Vector2.right;


        Vector3 worldWind =
            new Vector3(
                direction2.x,
                0f,
                direction2.y)
            *
            wind
            *
            maximumPrecipitationDrift;


        if (precipitationSystem != null
            &&
            precipitationSystem.isActiveAndEnabled)
        {
            precipitationSystem.SetState(
                targetWeather,
                rain
                *
                precipitationExposure,
                snow
                *
                precipitationExposure,
                worldWind);
        }


        UpdateAudio(
            audioExposure);


        if (windZone != null)
        {
            windZone.windMain =
                wind
                *
                maximumWindZoneStrength;
        }
    }


    // ============================================================
    // ART DIRECTION
    // ============================================================

    private static void GradeForWeather(
        ref WeatherVisuals visuals,
        WeatherType weather,
        float intensity)
    {
        intensity =
            Mathf.Clamp01(
                intensity);


        switch (weather)
        {
            // ----------------------------------------------------
            // ROMANTIC BLUE / PURPLE RAIN
            // ----------------------------------------------------

            case WeatherType.Rainy:

                visuals.cloudOvercast =
                    Mathf.Max(
                        visuals.cloudOvercast,
                        .84f);


                visuals.cloudThickness =
                    Mathf.Max(
                        visuals.cloudThickness,
                        .76f);


                // Not depressing / muddy.
                visuals.darkness =
                    Mathf.Clamp(
                        visuals.darkness,
                        .16f,
                        .25f);


                visuals.directLightMultiplier =
                    Mathf.Max(
                        visuals.directLightMultiplier,
                        .70f);


                visuals.ambientMultiplier =
                    Mathf.Max(
                        visuals.ambientMultiplier,
                        .91f);


                visuals.windStrength =
                    Mathf.Max(
                        visuals.windStrength,
                        .54f);


                visuals.cloudColor =
                    new Color(
                        .72f,
                        .75f,
                        .91f);


                visuals.fogColor =
                    new Color(
                        .55f,
                        .58f,
                        .75f);


                // Normal rain = no heavy world fog.
                visuals.fogDensity =
                    0f;

                break;


            // ----------------------------------------------------
            // RAIN STORM
            // ----------------------------------------------------

            case WeatherType.Storm:

                visuals.cloudOvercast =
                    Mathf.Max(
                        visuals.cloudOvercast,
                        .97f);


                visuals.cloudThickness =
                    Mathf.Max(
                        visuals.cloudThickness,
                        .94f);


                // Still dramatic, but not pitch black at night.
                visuals.darkness =
                    Mathf.Clamp(
                        visuals.darkness,
                        .46f,
                        .58f);


                visuals.directLightMultiplier =
                    Mathf.Max(
                        visuals.directLightMultiplier,
                        .42f);


                visuals.ambientMultiplier =
                    Mathf.Max(
                        visuals.ambientMultiplier,
                        .72f);


                visuals.windStrength =
                    Mathf.Max(
                        visuals.windStrength,
                        .96f);


                visuals.cloudColor =
                    new Color(
                        .43f,
                        .48f,
                        .67f);


                visuals.fogColor =
                    new Color(
                        .29f,
                        .34f,
                        .48f);


                visuals.fogDensity =
                    Mathf.Max(
                        visuals.fogDensity,
                        .011f);

                break;


            // ----------------------------------------------------
            // NORMAL SNOW
            // ----------------------------------------------------

            case WeatherType.Snowy:

                visuals.cloudOvercast =
                    Mathf.Max(
                        visuals.cloudOvercast,
                        .80f);


                visuals.cloudThickness =
                    Mathf.Max(
                        visuals.cloudThickness,
                        .74f);


                visuals.darkness =
                    Mathf.Min(
                        visuals.darkness,
                        .07f);


                visuals.directLightMultiplier =
                    Mathf.Max(
                        visuals.directLightMultiplier,
                        .78f);


                visuals.ambientMultiplier =
                    Mathf.Max(
                        visuals.ambientMultiplier,
                        .96f);


                visuals.cloudColor =
                    new Color(
                        .92f,
                        .94f,
                        .98f);


                visuals.fogColor =
                    new Color(
                        .79f,
                        .82f,
                        .88f);


                // Normal snow = no dense fog.
                visuals.fogDensity =
                    0f;


                visuals.windStrength =
                    Mathf.Max(
                        visuals.windStrength,
                        .36f);

                break;


            // ----------------------------------------------------
            // BLIZZARD
            // ----------------------------------------------------

            case WeatherType.SnowStorm:

                visuals.cloudOvercast =
                    Mathf.Max(
                        visuals.cloudOvercast,
                        .97f);


                visuals.cloudThickness =
                    Mathf.Max(
                        visuals.cloudThickness,
                        .95f);


                visuals.darkness =
                    Mathf.Clamp(
                        visuals.darkness,
                        .16f,
                        .24f);


                visuals.directLightMultiplier =
                    Mathf.Max(
                        visuals.directLightMultiplier,
                        .52f);


                visuals.ambientMultiplier =
                    Mathf.Max(
                        visuals.ambientMultiplier,
                        .82f);


                visuals.cloudColor =
                    new Color(
                        .78f,
                        .84f,
                        .92f);


                visuals.fogColor =
                    new Color(
                        .67f,
                        .72f,
                        .81f);


                visuals.fogDensity =
                    Mathf.Max(
                        visuals.fogDensity,
                        .022f);


                visuals.windStrength =
                    Mathf.Max(
                        visuals.windStrength,
                        .98f);

                break;


            case WeatherType.Foggy:

                visuals.fogDensity =
                    Mathf.Max(
                        visuals.fogDensity,
                        .026f);


                visuals.fogColor =
                    new Color(
                        .68f,
                        .72f,
                        .75f);


                visuals.windStrength =
                    Mathf.Min(
                        visuals.windStrength,
                        .28f);

                break;


            default:

                // Sunny / Partly / Cloudy / Overcast.
                visuals.fogDensity =
                    0f;

                break;
        }


        visuals.darkness *=
            Mathf.Lerp(
                .68f,
                1f,
                intensity);


        visuals.directLightMultiplier =
            Mathf.Lerp(
                1f,
                visuals.directLightMultiplier,
                Mathf.Lerp(
                    .50f,
                    1f,
                    intensity));


        visuals.cloudCoverage *=
            Mathf.Lerp(
                .86f,
                1f,
                intensity);


        visuals.cloudOvercast *=
            Mathf.Lerp(
                .82f,
                1f,
                intensity);


        visuals.cloudThickness *=
            Mathf.Lerp(
                .82f,
                1f,
                intensity);


        if (weather
                ==
                WeatherType.Storm
            ||
            weather
                ==
                WeatherType.SnowStorm
            ||
            weather
                ==
                WeatherType.Foggy)
        {
            visuals.fogDensity *=
                Mathf.Lerp(
                    .60f,
                    1f,
                    intensity);
        }
        else
        {
            visuals.fogDensity =
                0f;
        }
    }


    // ============================================================
    // FALLBACK VISUALS
    // ============================================================

    private static WeatherVisuals DefaultVisualsFor(
        WeatherType weather)
    {
        WeatherVisuals v =
            new WeatherVisuals
            {
                skyTopColor =
                    new Color(
                        .32f,
                        .65f,
                        .87f),

                skyBottomColor =
                    new Color(
                        .64f,
                        .84f,
                        .96f),

                cloudColor =
                    Color.white,

                cloudCoverage =
                    .25f,

                fogDensity =
                    0f,

                fogColor =
                    new Color(
                        .70f,
                        .85f,
                        .95f),

                darkness =
                    0f,

                directLightMultiplier =
                    1f,

                ambientMultiplier =
                    1f,

                windStrength =
                    .10f,

                cloudOvercast =
                    0f,

                cirrusAmount =
                    .18f,

                cloudThickness =
                    .20f
            };


        if (weather == WeatherType.PartlyCloudy
            ||
            weather == WeatherType.Cloudy)
        {
            v.cloudCoverage =
                .48f;

            v.cloudOvercast =
                .15f;

            v.cloudThickness =
                .38f;

            v.windStrength =
                .28f;
        }


        if (weather == WeatherType.Overcast)
        {
            v.cloudCoverage =
                .78f;

            v.cloudOvercast =
                .78f;

            v.cloudThickness =
                .72f;

            v.darkness =
                .18f;

            v.directLightMultiplier =
                .78f;

            v.ambientMultiplier =
                .90f;

            v.windStrength =
                .40f;
        }


        if (weather == WeatherType.Rainy)
        {
            v.cloudCoverage =
                .90f;

            v.cloudOvercast =
                .86f;

            v.cloudThickness =
                .78f;

            v.darkness =
                .21f;

            v.directLightMultiplier =
                .72f;

            v.ambientMultiplier =
                .93f;

            v.windStrength =
                .56f;
        }


        if (weather == WeatherType.Storm)
        {
            v.cloudCoverage =
                1f;

            v.cloudOvercast =
                .98f;

            v.cloudThickness =
                .96f;

            v.darkness =
                .54f;

            v.directLightMultiplier =
                .44f;

            v.ambientMultiplier =
                .74f;

            v.windStrength =
                1f;
        }


        if (weather == WeatherType.Snowy)
        {
            v.cloudCoverage =
                .90f;

            v.cloudOvercast =
                .82f;

            v.cloudThickness =
                .76f;

            v.darkness =
                .05f;

            v.directLightMultiplier =
                .80f;

            v.ambientMultiplier =
                .97f;

            v.windStrength =
                .38f;
        }


        if (weather == WeatherType.SnowStorm)
        {
            v.cloudCoverage =
                1f;

            v.cloudOvercast =
                .98f;

            v.cloudThickness =
                .96f;

            v.darkness =
                .20f;

            v.directLightMultiplier =
                .54f;

            v.ambientMultiplier =
                .84f;

            v.windStrength =
                1f;
        }


        return
            v;
    }


    // ============================================================
    // BLEND
    // ============================================================

    private static WeatherVisuals Blend(
        WeatherVisuals a,
        WeatherVisuals b,
        float t)
    {
        return
            new WeatherVisuals
            {
                skyTopColor =
                    Color.Lerp(
                        a.skyTopColor,
                        b.skyTopColor,
                        t),

                skyBottomColor =
                    Color.Lerp(
                        a.skyBottomColor,
                        b.skyBottomColor,
                        t),

                cloudColor =
                    Color.Lerp(
                        a.cloudColor,
                        b.cloudColor,
                        t),

                cloudCoverage =
                    Mathf.Lerp(
                        a.cloudCoverage,
                        b.cloudCoverage,
                        t),

                fogDensity =
                    Mathf.Lerp(
                        a.fogDensity,
                        b.fogDensity,
                        t),

                fogColor =
                    Color.Lerp(
                        a.fogColor,
                        b.fogColor,
                        t),

                darkness =
                    Mathf.Lerp(
                        a.darkness,
                        b.darkness,
                        t),

                directLightMultiplier =
                    Mathf.Lerp(
                        a.directLightMultiplier,
                        b.directLightMultiplier,
                        t),

                ambientMultiplier =
                    Mathf.Lerp(
                        a.ambientMultiplier,
                        b.ambientMultiplier,
                        t),

                windStrength =
                    Mathf.Lerp(
                        a.windStrength,
                        b.windStrength,
                        t),

                cloudOvercast =
                    Mathf.Lerp(
                        a.cloudOvercast,
                        b.cloudOvercast,
                        t),

                cirrusAmount =
                    Mathf.Lerp(
                        a.cirrusAmount,
                        b.cirrusAmount,
                        t),

                cloudThickness =
                    Mathf.Lerp(
                        a.cloudThickness,
                        b.cloudThickness,
                        t)
            };
    }


    // ============================================================
    // AUDIO
    // ============================================================

    private void UpdateAudio(
        float exposure)
    {
        exposure =
            Mathf.Clamp01(
                exposure);


        float rainShelter =
            Mathf.Lerp(
                indoorRainVolume,
                1f,
                exposure);


        float windShelter =
            Mathf.Lerp(
                indoorWindVolume,
                1f,
                exposure);


        if (rainSource != null)
        {
            rainSource.volume =
                rain
                *
                maxRainVolume
                *
                rainShelter;


            rainSource.pitch =
                Mathf.Lerp(
                    1.06f,
                    .96f,
                    rain);


            ToggleLoop(
                rainSource,
                rain > .002f
                &&
                rainLoop != null);
        }


        if (windSource != null)
        {
            windSource.volume =
                Mathf.Clamp01(
                    wind)
                *
                maxWindVolume
                *
                windShelter;


            windSource.pitch =
                Mathf.Lerp(
                    .92f,
                    1.08f,
                    wind);


            ToggleLoop(
                windSource,
                wind > .04f
                &&
                windLoop != null);
        }


        float cutoff =
            Mathf.Lerp(
                indoorLowPassHz,
                22000f,
                exposure);


        ApplyLowPass(
            rainLowPass,
            cutoff,
            exposure);


        ApplyLowPass(
            windLowPass,
            cutoff,
            exposure);


        ApplyLowPass(
            thunderLowPass,
            cutoff,
            exposure);
    }


    // ============================================================
    // STORM
    // ============================================================

    private void UpdateThunderstormState()
    {
        bool shouldRun =
            targetWeather
            ==
            WeatherType.Storm
            &&
            rain
            >=
            stormStartRainThreshold;


        if (shouldRun)
        {
            if (stormCoroutine == null)
            {
                stormCoroutine =
                    StartCoroutine(
                        ThunderstormRoutine());
            }
        }
        else if (stormCoroutine != null)
        {
            StopThunderstorm();
        }
    }


    private IEnumerator ThunderstormRoutine()
    {
        while (true)
        {
            float minInterval =
                Mathf.Max(
                    .1f,
                    Mathf.Min(
                        lightningInterval.x,
                        lightningInterval.y));


            float maxInterval =
                Mathf.Max(
                    minInterval,
                    Mathf.Max(
                        lightningInterval.x,
                        lightningInterval.y));


            yield return
                new WaitForSeconds(
                    Random.Range(
                        minInterval,
                        maxInterval));


            SetLightningFlash(
                1f,
                1f);


            yield return
                new WaitForSeconds(
                    .085f);


            SetLightningFlash(
                0f,
                0f);


            yield return
                new WaitForSeconds(
                    .045f);


            SetLightningFlash(
                .62f,
                .72f);


            yield return
                new WaitForSeconds(
                    .075f);


            SetLightningFlash(
                0f,
                0f);


            float minDelay =
                Mathf.Max(
                    0f,
                    Mathf.Min(
                        thunderDelay.x,
                        thunderDelay.y));


            float maxDelay =
                Mathf.Max(
                    minDelay,
                    Mathf.Max(
                        thunderDelay.x,
                        thunderDelay.y));


            yield return
                new WaitForSeconds(
                    Random.Range(
                        minDelay,
                        maxDelay));


            if (thunderClips != null
                &&
                thunderClips.Length > 0)
            {
                AudioClip clip =
                    thunderClips[
                        Random.Range(
                            0,
                            thunderClips.Length)];


                if (clip != null
                    &&
                    thunderSource != null)
                {
                    float exposure =
                        exposureController != null

                            ?
                            exposureController.AudioExposure

                            :
                            1f;


                    float shelter =
                        Mathf.Lerp(
                            indoorThunderVolume,
                            1f,
                            exposure);


                    thunderSource.volume =
                        maxThunderVolume
                        *
                        shelter;


                    thunderSource.pitch =
                        Random.Range(
                            .94f,
                            1.035f);


                    thunderSource.PlayOneShot(
                        clip,
                        Random.Range(
                            .88f,
                            1f));
                }
            }
        }
    }


    private void StopThunderstorm()
    {
        if (stormCoroutine != null)
        {
            StopCoroutine(
                stormCoroutine);


            stormCoroutine =
                null;
        }


        SetLightningFlash(
            0f,
            0f);


        if (thunderSource != null)
        {
            thunderSource.Stop();
        }
    }


    private void SetLightningFlash(
        float lightMultiplier,
        float skyMultiplier)
    {
        if (lightningLight != null)
        {
            lightningLight.intensity =
                lightningIntensity
                *
                Mathf.Max(
                    0f,
                    lightMultiplier);
        }


        if (skyController != null)
        {
            skyController.SetLightningFlash(
                Mathf.Clamp01(
                    skyMultiplier
                    *
                    skyLightningFlashStrength));
        }
    }


    // ============================================================
    // AUDIO HELPERS
    // ============================================================

    private AudioSource CreateLoopSource(
        string objectName,
        AudioClip clip,
        out AudioLowPassFilter lowPass)
    {
        GameObject child =
            new GameObject(
                objectName);


        child.transform.SetParent(
            transform,
            false);


        AudioSource source =
            child.AddComponent<
                AudioSource>();


        source.loop =
            true;


        source.playOnAwake =
            false;


        source.spatialBlend =
            0f;


        source.clip =
            clip;


        source.volume =
            0f;


        lowPass =
            child.AddComponent<
                AudioLowPassFilter>();


        lowPass.enabled =
            false;


        return
            source;
    }


    private AudioSource CreateOneShotSource(
        string objectName,
        out AudioLowPassFilter lowPass)
    {
        GameObject child =
            new GameObject(
                objectName);


        child.transform.SetParent(
            transform,
            false);


        AudioSource source =
            child.AddComponent<
                AudioSource>();


        source.loop =
            false;


        source.playOnAwake =
            false;


        source.spatialBlend =
            0f;


        source.volume =
            1f;


        lowPass =
            child.AddComponent<
                AudioLowPassFilter>();


        lowPass.enabled =
            false;


        return
            source;
    }


    private static void ToggleLoop(
        AudioSource source,
        bool shouldPlay)
    {
        if (source == null)
            return;


        if (shouldPlay
            &&
            source.clip != null)
        {
            if (!source.isPlaying)
            {
                source.Play();
            }
        }
        else if (source.isPlaying)
        {
            source.Stop();
        }
    }


    private static void StopSource(
        AudioSource source)
    {
        if (source != null
            &&
            source.isPlaying)
        {
            source.Stop();
        }
    }


    private static void ApplyLowPass(
        AudioLowPassFilter filter,
        float cutoff,
        float exposure)
    {
        if (filter == null)
            return;


        filter.cutoffFrequency =
            Mathf.Clamp(
                cutoff,
                500f,
                22000f);


        filter.enabled =
            exposure < .995f;
    }
}