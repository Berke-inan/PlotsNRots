using System.Collections;
using UnityEngine;
using PlotNRots.SaveSystem;

[System.Serializable]
public struct WeatherVisuals
{
    [Header("Gökyüzü Ayarları")]
    public Color skyTopColor;
    public Color skyBottomColor;
    public Color cloudColor;
    [Range(0f, 1f)] public float cloudCoverage;

    [Header("Atmosfer Ayarları")]
    public float fogDensity;
    public Color fogColor;
    [Range(0, 1)] public float darkness;
    [Range(0, 1)] public float directLightMultiplier;
    [Range(0, 1)] public float ambientMultiplier;
    [Range(0, 1)] public float windStrength;
    [Range(0, 1)] public float cloudOvercast;
    [Range(0, 1)] public float cirrusAmount;
    [Range(0, 1)] public float cloudThickness;
}

public class WeatherVisualsManager : MonoBehaviour
{
    [Header("Dinamik Yağış Efektleri")]
    [SerializeField] private ParticleSystem rainParticles;
    [SerializeField] private ParticleSystem snowParticles;

    [SerializeField] private float maxRainEmission = 100f;
    [SerializeField] private float maxSnowEmission = 100f;

    [Header("Yağmur Sesi Ayarları")]
    [Tooltip("Döngüye girecek (Loop) şiddetli yağmur sesi")]
    [SerializeField] private AudioClip rainSoundClip;
    [Tooltip("En şiddetli fırtınada yağmurun çıkacağı maksimum ses seviyesi")]
    [SerializeField, Range(0f, 1f)] private float maxRainVolume = 0.8f;

    private AudioSource rainAudioSource;


    [Header("Gök Gürültüsü ve Şimşek")]
    [SerializeField] private Light lightningLight;
    [SerializeField] private AudioClip[] thunderSounds;
    private AudioSource thunderAudioSource;

    [Header("Geçiş Ayarları")]


    // --- ÖN TANIMLI RENK VE GÖRSELLİK AYARLARI ---
    [SerializeField]
    private WeatherVisuals sunnyVisuals = new WeatherVisuals
    {
        skyTopColor = new Color(0.32f, 0.65f, 0.87f),
        skyBottomColor = new Color(0.64f, 0.84f, 0.96f),
        cloudColor = new Color(1f, 1f, 1f),
        cloudCoverage = 0.25f,
        fogDensity = 0.0005f,
        fogColor = new Color(0.7f, 0.85f, 0.95f)
    };

    [SerializeField]
    private WeatherVisuals cloudyVisuals = new WeatherVisuals
    {
        skyTopColor = new Color(0.46f, 0.54f, 0.6f),
        skyBottomColor = new Color(0.65f, 0.71f, 0.75f),
        cloudColor = new Color(0.85f, 0.85f, 0.85f),
        cloudCoverage = 0.55f,
        fogDensity = 0.005f,
        fogColor = new Color(0.65f, 0.71f, 0.75f)
    };

    [SerializeField]
    private WeatherVisuals rainyVisuals = new WeatherVisuals
    {
        skyTopColor = new Color(0.23f, 0.26f, 0.3f),
        skyBottomColor = new Color(0.3f, 0.34f, 0.38f),
        cloudColor = new Color(0.25f, 0.28f, 0.32f),
        cloudCoverage = 0.85f,
        fogDensity = 0.008f,
        fogColor = new Color(0.3f, 0.34f, 0.38f)
    };

    [SerializeField]
    private WeatherVisuals snowyVisuals = new WeatherVisuals
    {
        skyTopColor = new Color(0.64f, 0.69f, 0.72f),
        skyBottomColor = new Color(0.76f, 0.82f, 0.85f),
        cloudColor = new Color(0.9f, 0.92f, 0.95f),
        cloudCoverage = 0.8f,
        fogDensity = 0.018f,
        fogColor = new Color(0.8f, 0.84f, 0.88f)
    };

    [Header("Transition / day-night")]
    [SerializeField, Min(0)] private float transitionDuration = 20;
    [SerializeField, Range(0, 0.9f)] private float precipitationDelayFraction = 0.3f;
    [SerializeField, Range(0, 1)] private float nightBrightness = 0.12f;
    [SerializeField] private DayNightCycleManager clock;
    [Header("Wind / emitter follow (optional)")]
    [SerializeField] private WindZone windZone;
    [SerializeField] private AudioSource windAudio;
    [SerializeField, Min(0)] private float maximumWind = 2;
    [SerializeField, Min(0)] private float maximumParticleDrift = 6;
    [SerializeField] private Transform precipitationAnchor;

    [Header("Yağış Hareketi")]
    [SerializeField] private Vector2 precipitationWindDirection = new Vector2(1f, 0.3f);
    [SerializeField, Min(0.1f)] private float rainFallSpeed = 24f;
    [SerializeField, Min(0.1f)] private float snowFallSpeed = 3.5f;
    [SerializeField, Range(0f, 1f)] private float snowWindMultiplier = 0.65f;

    [Header("Yağış Hacmi / Araç Hızı")]
    [Tooltip("İnce bir yağış tabakası kameranın üstünde durur; X/Z alanı görüş yönünden bağımsız çevreyi kapsar.")]
    [SerializeField] private Vector3 rainVolumeSize = new Vector3(60f, 3f, 60f);
    [SerializeField, Min(0f)] private float rainVolumeYOffset = 18f;
    [Tooltip("Kar yavaş düştüğü için daha geniş bir üst hacim kullanır.")]
    [SerializeField] private Vector3 snowVolumeSize = new Vector3(70f, 5f, 70f);
    [SerializeField, Min(0f)] private float snowVolumeYOffset = 14f;

    [Header("Yağış Görsel Yoğunluğu")]
    [Tooltip("Scene'deki 100/s temel emission geniş hacimde seyrek kalır; görsel yoğunluğu alanı dolduracak kadar artırır.")]
    [SerializeField, Range(1f, 10f)] private float rainDensityMultiplier = 6f;
    [SerializeField, Range(1f, 10f)] private float snowDensityMultiplier = 4f;
    [SerializeField, Min(.01f)] private float rainParticleWidth = .075f;
    [SerializeField, Min(.1f)] private float rainParticleLifetime = 2.25f;
    [SerializeField, Min(.1f)] private float rainStretchLength = 2.0f;
    [SerializeField] private Vector2 snowParticleSizeRange = new Vector2(.08f, .22f);
    [SerializeField, Min(.1f)] private float snowParticleLifetime = 9f;

    [Header("Fırtına / Tipi Şiddeti")]
    [SerializeField, Range(1f, 3f)] private float rainStormEmissionMultiplier = 1.6f;
    [SerializeField, Range(1f, 3f)] private float rainStormFallSpeedMultiplier = 1.35f;
    [SerializeField, Range(1f, 3f)] private float snowStormEmissionMultiplier = 2.0f;
    [SerializeField, Range(1f, 3f)] private float snowStormFallSpeedMultiplier = 1.7f;
    [SerializeField, Range(1f, 3f)] private float stormWindMultiplier = 1.6f;
    [SerializeField, Range(1f, 3f)] private float rainStormFogMultiplier = 1.3f;
    [SerializeField, Range(1f, 3f)] private float snowStormFogMultiplier = 1.8f;

    [Header("Storm")]
    [SerializeField, Range(0, 1)] private float stormThreshold = 0.6f;
    [SerializeField] private Vector2 lightningInterval = new Vector2(10, 30);
    [SerializeField] private Vector2 thunderDelay = new Vector2(1, 3);
    [SerializeField, Min(0)] private float lightningIntensity = 3;
    [SerializeField, Range(0f, 1f)] private float skyLightningFlashStrength = 0.85f;

    [Header("Sky and appearance data")]
    [SerializeField] private StylizedSkyController skyController;
    [SerializeField] private StylizedCloudManager cloudManager;
    [SerializeField] private WeatherAppearanceProfile appearanceProfile;
    private WeatherVisuals current, from, target;
    private float rain, snow, wind, fromRain, fromSnow, fromWind, targetRain, targetSnow, targetWind;
    private float elapsed;
    private bool transitioning, started;
    private Coroutine stormCoroutine;
    private Material originalSky, runtimeSky;
    private float baseCloudSpeed, cloudOffset;
    private Vector3 rainOffset, snowOffset;
    private bool originalFog;
    private FogMode originalFogMode;
    private Color originalFogColor;
    private float originalFogDensity;

    private void Awake()
    {
        if (skyController == null) skyController = GetComponent<StylizedSkyController>();
        if (cloudManager == null) cloudManager = GetComponent<StylizedCloudManager>();
        ConfigurePrecipitationVolume(rainParticles, true);
        ConfigurePrecipitationVolume(snowParticles, false);
        thunderAudioSource = gameObject.AddComponent<AudioSource>();
        thunderAudioSource.spatialBlend = 0;
        thunderAudioSource.playOnAwake = false;
        rainAudioSource = gameObject.AddComponent<AudioSource>();
        rainAudioSource.spatialBlend = 0;
        rainAudioSource.loop = true;
        rainAudioSource.playOnAwake = false;
        rainAudioSource.clip = rainSoundClip;
        rainAudioSource.volume = 0;
    }
    private void OnEnable()
    {
        SeasonManager.OnWeatherChanged += HandleWeatherChange;
        SaveManager.OnGameLoaded += ApplyWeatherImmediate;
        originalSky = RenderSettings.skybox;
        if (originalSky != null && skyController == null)
        {
            runtimeSky = new Material(originalSky);
            RenderSettings.skybox = runtimeSky;
            baseCloudSpeed = runtimeSky.HasProperty("_CloudSpeed") ? runtimeSky.GetFloat("_CloudSpeed") : 0.015f;
        }
        originalFog = RenderSettings.fog;
        originalFogMode = RenderSettings.fogMode;
        originalFogColor = RenderSettings.fogColor;
        originalFogDensity = RenderSettings.fogDensity;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        if (started) ApplyWeatherImmediate();
    }
    private void Start()
    {
        started = true;
        if (clock == null) clock = DayNightCycleManager.Instance;
        if (precipitationAnchor != null)
        {
            if (rainParticles != null) rainOffset = rainParticles.transform.position - precipitationAnchor.position;
            if (snowParticles != null) snowOffset = snowParticles.transform.position - precipitationAnchor.position;
        }
        ApplyWeatherImmediate();
    }
    private void OnDisable()
    {
        SeasonManager.OnWeatherChanged -= HandleWeatherChange;
        SaveManager.OnGameLoaded -= ApplyWeatherImmediate;
        StopStorm();
        if (rainAudioSource != null) rainAudioSource.Stop();
        if (windAudio != null) windAudio.Stop();
        if (windZone != null) windZone.windMain = 0;
        if (rainParticles != null) rainParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (snowParticles != null) snowParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (RenderSettings.skybox == runtimeSky) RenderSettings.skybox = originalSky;
        if (runtimeSky != null) { if (Application.isPlaying) Destroy(runtimeSky); else DestroyImmediate(runtimeSky); }
        runtimeSky = null;
        RenderSettings.fog = originalFog;
        RenderSettings.fogMode = originalFogMode;
        RenderSettings.fogColor = originalFogColor;
        RenderSettings.fogDensity = originalFogDensity;
    }
    private void HandleWeatherChange(WeatherType weather)
    {
        if (!started) return;
        if (SeasonManager.Instance != null && SeasonManager.Instance.IsRestoringState) ApplyWeatherImmediate();
        else TransitionToWeather();
    }
    private void ReadTarget()
    {
        var manager = SeasonManager.Instance;
        var weather = manager != null ? manager.currentWeather : WeatherType.Sunny;
        float intensity = manager != null ? manager.WeatherIntensity : 0;
        target = WeatherRules.IsRain(weather) ? rainyVisuals : WeatherRules.IsSnow(weather) ? snowyVisuals : weather == WeatherType.Sunny ? sunnyVisuals : cloudyVisuals;
        target.directLightMultiplier = target.ambientMultiplier = 1;
        target.darkness = WeatherRules.IsRain(weather) ? .7f : WeatherRules.IsSnow(weather) ? .4f : weather == WeatherType.Sunny ? 0 : .25f;
        target.windStrength = weather == WeatherType.Sunny ? .1f : .6f;
        if (appearanceProfile != null && appearanceProfile.TryGet(weather, out var appearance)) target = appearance;

        // Runtime art-direction guardrails. The appearance asset remains the source,
        // these only make the precipitation families read clearly and consistently.
        if (weather == WeatherType.Rainy)
        {
            target.cloudOvercast = Mathf.Max(target.cloudOvercast, .82f);
            target.cloudThickness = Mathf.Max(target.cloudThickness, .78f);
            target.darkness = Mathf.Max(target.darkness, .52f);
            target.windStrength = Mathf.Max(target.windStrength, .58f);
        }
        else if (weather == WeatherType.Storm)
        {
            target.cloudOvercast = Mathf.Max(target.cloudOvercast, .96f);
            target.cloudThickness = Mathf.Max(target.cloudThickness, .94f);
            target.darkness = Mathf.Max(target.darkness, .78f);
            target.windStrength = Mathf.Max(target.windStrength, .9f);
            target.fogDensity *= rainStormFogMultiplier;
        }
        else if (weather == WeatherType.Snowy)
        {
            // Bright, cold, diffuse overcast. Snow should never read like a dark rain preset.
            target.cloudOvercast = Mathf.Max(target.cloudOvercast, .80f);
            target.cloudThickness = Mathf.Max(target.cloudThickness, .72f);
            target.darkness = Mathf.Min(target.darkness, .08f);
            target.directLightMultiplier = Mathf.Max(target.directLightMultiplier, .72f);
            target.ambientMultiplier = Mathf.Max(target.ambientMultiplier, .95f);
            target.cloudColor = Color.Lerp(target.cloudColor, new Color(1.08f, 1.11f, 1.16f), .72f);
            target.fogColor = Color.Lerp(target.fogColor, new Color(.86f, .91f, .96f), .72f);
            target.fogDensity = Mathf.Clamp(target.fogDensity, .008f, .014f);
            target.windStrength = Mathf.Max(target.windStrength, .38f);
        }
        else if (weather == WeatherType.SnowStorm)
        {
            // Blizzard: brighter than a rain storm, but with much denser haze and wind-driven snow.
            target.cloudOvercast = Mathf.Max(target.cloudOvercast, .97f);
            target.cloudThickness = Mathf.Max(target.cloudThickness, .94f);
            target.darkness = Mathf.Clamp(target.darkness, .16f, .26f);
            target.directLightMultiplier = Mathf.Max(target.directLightMultiplier, .48f);
            target.ambientMultiplier = Mathf.Max(target.ambientMultiplier, .80f);
            target.windStrength = Mathf.Max(target.windStrength, .98f);
            target.cloudColor = Color.Lerp(target.cloudColor, new Color(1.02f, 1.07f, 1.14f), .70f);
            target.fogColor = Color.Lerp(target.fogColor, new Color(.82f, .88f, .94f), .72f);
            target.fogDensity = Mathf.Max(target.fogDensity * snowStormFogMultiplier, .026f);
        }

        target.darkness *= Mathf.Lerp(.65f, 1, intensity);
        target.directLightMultiplier = Mathf.Lerp(1, target.directLightMultiplier, Mathf.Lerp(.5f, 1, intensity));
        target.fogDensity *= Mathf.Lerp(.75f, 1, intensity);
        target.cloudCoverage *= Mathf.Lerp(.85f, 1, intensity);
        target.cloudOvercast *= Mathf.Lerp(.82f, 1, intensity);
        target.cloudThickness *= Mathf.Lerp(.8f, 1, intensity);
        targetRain = WeatherRules.IsRain(weather) ? intensity : 0;
        targetSnow = WeatherRules.IsSnow(weather) ? intensity : 0;
        targetWind = target.windStrength * Mathf.Lerp(.5f, 1, intensity);

    }
    public void TransitionToWeather()
    {
        StopStorm();
        ReadTarget();
        from = current; fromRain = rain; fromSnow = snow; fromWind = wind;
        elapsed = 0;
        transitioning = true;
        if (transitionDuration <= 0) ApplyWeatherImmediate();
    }
    public void ApplyWeatherImmediate()
    {
        if (!isActiveAndEnabled) return;
        StopStorm();
        ReadTarget();
        current = target; rain = targetRain; snow = targetSnow; wind = targetWind;
        transitioning = false;
        // Clear old particles: loading Clear must not leave a previous snow/rain cloud.
        if (rainParticles != null) rainParticles.Clear(true);
        if (snowParticles != null) snowParticles.Clear(true);
        ApplyVisuals(true);
        RefreshStorm();
    }
    private void LateUpdate()
    {
        if (transitioning)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, transitionDuration));
            float atmosphere = Mathf.SmoothStep(0, 1, t);
            float precipitation = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(precipitationDelayFraction, 1, t));
            current = Blend(from, target, atmosphere);
            rain = Mathf.Lerp(fromRain, targetRain, targetRain > fromRain ? precipitation : atmosphere);
            snow = Mathf.Lerp(fromSnow, targetSnow, targetSnow > fromSnow ? precipitation : atmosphere);
            wind = Mathf.Lerp(fromWind, targetWind, precipitation);
            if (t >= 1) { transitioning = false; RefreshStorm(); }
        }
        cloudOffset += Time.deltaTime * baseCloudSpeed * Mathf.Lerp(0.5f, 2, wind);
        ApplyVisuals(); // Time modulation continues after the weather transition ends.
        if (precipitationAnchor != null)
        {
            if (rainParticles != null) rainParticles.transform.position = precipitationAnchor.position + rainOffset;
            if (snowParticles != null) snowParticles.transform.position = precipitationAnchor.position + snowOffset;
        }
    }
    private static WeatherVisuals Blend(WeatherVisuals a, WeatherVisuals b, float t) => new WeatherVisuals
    {
        skyTopColor = Color.Lerp(a.skyTopColor, b.skyTopColor, t),
        skyBottomColor = Color.Lerp(a.skyBottomColor, b.skyBottomColor, t),
        cloudColor = Color.Lerp(a.cloudColor, b.cloudColor, t),
        cloudCoverage = Mathf.Lerp(a.cloudCoverage, b.cloudCoverage, t),
        fogColor = Color.Lerp(a.fogColor, b.fogColor, t),
        fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, t),
        darkness = Mathf.Lerp(a.darkness, b.darkness, t),
        directLightMultiplier = Mathf.Lerp(a.directLightMultiplier, b.directLightMultiplier, t),
        ambientMultiplier = Mathf.Lerp(a.ambientMultiplier, b.ambientMultiplier, t),
        windStrength = Mathf.Lerp(a.windStrength, b.windStrength, t),
        cloudOvercast = Mathf.Lerp(a.cloudOvercast, b.cloudOvercast, t),
        cirrusAmount = Mathf.Lerp(a.cirrusAmount, b.cirrusAmount, t),
        cloudThickness = Mathf.Lerp(a.cloudThickness, b.cloudThickness, t)
    };
    private void ApplyVisuals(bool immediate = false)
    {
        if (skyController != null && skyController.isActiveAndEnabled)
        {
            if (immediate) skyController.ApplyWeather(current, wind);
            else skyController.SetWeather(current, wind);
        }
        else
        {
            float brightness = Mathf.Lerp(nightBrightness, 1, clock != null ? clock.Daylight : 1);
            RenderSettings.fogColor = Dim(current.fogColor, brightness);
            RenderSettings.fogDensity = current.fogDensity;
            SetSkyColor("_SkyTopColor", Dim(current.skyTopColor, brightness));
            SetSkyColor("_SkyBottomColor", Dim(current.skyBottomColor, brightness));
            SetSkyColor("_CloudHighlightColor", Dim(current.cloudColor, brightness));
            SetSkyColor("_CloudBaseColor", Dim(current.cloudColor * .78f, brightness));
            if (runtimeSky != null && runtimeSky.HasProperty("_CloudCoverage"))
            {
                runtimeSky.SetFloat("_CloudCoverage", current.cloudCoverage);
                runtimeSky.SetFloat("_CloudOvercast", current.cloudOvercast);
                runtimeSky.SetFloat("_CirrusAmount", current.cirrusAmount);
                runtimeSky.SetFloat("_CloudThickness", current.cloudThickness);
            }
        }
        if (cloudManager != null && cloudManager.isActiveAndEnabled)
        {
            if (immediate) cloudManager.ApplyWeather(current, wind);
            else cloudManager.SetWeather(current, wind);
        }
        ApplyParticles(rainParticles, maxRainEmission, rain);
        ApplyParticles(snowParticles, maxSnowEmission, snow);
        rainAudioSource.volume = rain * maxRainVolume;
        rainAudioSource.pitch = Mathf.Lerp(1.2f, 1, rain);
        if (rain > 0.001f && rainAudioSource.clip != null && !rainAudioSource.isPlaying) rainAudioSource.Play();
        if (rain <= 0.001f && rainAudioSource.isPlaying) rainAudioSource.Stop();
        if (windZone != null) windZone.windMain = wind * maximumWind;
        if (windAudio != null)
        {
            windAudio.volume = wind;
            if (wind > 0.001f && windAudio.clip != null && !windAudio.isPlaying) windAudio.Play();
        }
    }
    private static Color Dim(Color color, float brightness) { color *= brightness; color.a = 1; return color; }
    private void SetSkyColor(string property, Color color) { if (runtimeSky != null && runtimeSky.HasProperty(property)) runtimeSky.SetColor(property, color); }
    private void ApplyParticles(ParticleSystem particles, float maximum, float intensity)
    {
        if (particles == null) return;

        bool isRain = particles == rainParticles;
        WeatherType weather = SeasonManager.Instance != null ? SeasonManager.Instance.currentWeather : WeatherType.Sunny;
        bool rainStorm = isRain && weather == WeatherType.Storm;
        bool snowStorm = !isRain && weather == WeatherType.SnowStorm;
        bool storm = rainStorm || snowStorm;

        // The rig follows the camera, while existing particles stay in world space.
        // Start Speed = 0 prevents the authored Shape/Transform forward axis from
        // pushing precipitation toward one side of the camera.
        var main = particles.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeedMultiplier = 0f;

        float densityMultiplier = isRain ? rainDensityMultiplier : snowDensityMultiplier;
        float emissionMultiplier = rainStorm ? rainStormEmissionMultiplier
            : snowStorm ? snowStormEmissionMultiplier : 1f;
        float fallMultiplier = rainStorm ? rainStormFallSpeedMultiplier
            : snowStorm ? snowStormFallSpeedMultiplier : 1f;
        float stormDrift = storm ? stormWindMultiplier : 1f;

        var emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = maximum * intensity * densityMultiplier * emissionMultiplier;

        Vector2 windDirection = precipitationWindDirection.sqrMagnitude > 0.0001f
            ? precipitationWindDirection.normalized
            : Vector2.right;

        float drift = wind * maximumParticleDrift
            * (isRain ? 1f : snowWindMultiplier)
            * stormDrift;
        float fallSpeed = (isRain
            ? rainFallSpeed * Mathf.Lerp(.80f, 1.15f, intensity)
            : snowFallSpeed * Mathf.Lerp(.75f, 1.10f, intensity))
            * fallMultiplier;

        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(windDirection.x * drift);
        velocity.y = new ParticleSystem.MinMaxCurve(-fallSpeed);
        velocity.z = new ParticleSystem.MinMaxCurve(windDirection.y * drift);

        if (intensity > 0.001f && !particles.isPlaying)
            particles.Play();
        else if (intensity <= 0.001f && particles.isPlaying)
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void ConfigurePrecipitationVolume(ParticleSystem particles, bool isRain)
    {
        if (particles == null) return;

        var main = particles.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeedMultiplier = 0f;

        if (isRain)
        {
            main.startLifetime = new ParticleSystem.MinMaxCurve(rainParticleLifetime);
            main.startSize = new ParticleSystem.MinMaxCurve(rainParticleWidth);
        }
        else
        {
            main.startLifetime = new ParticleSystem.MinMaxCurve(snowParticleLifetime);
            float minSize = Mathf.Max(.01f, Mathf.Min(snowParticleSizeRange.x, snowParticleSizeRange.y));
            float maxSize = Mathf.Max(minSize, Mathf.Max(snowParticleSizeRange.x, snowParticleSizeRange.y));
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        }

        // Spawn from a thin slab ABOVE the camera, not through the whole camera-height volume.
        // This makes every flake/drop enter the view from above and prevents the "one side only" feel.
        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = new Vector3(0f, isRain ? rainVolumeYOffset : snowVolumeYOffset, 0f);
        shape.rotation = Vector3.zero;
        shape.scale = isRain ? rainVolumeSize : snowVolumeSize;

        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            if (isRain)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.cameraVelocityScale = 0f;
                renderer.velocityScale = .04f;
                renderer.lengthScale = rainStretchLength;
            }
            else
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.cameraVelocityScale = 0f;
            }
        }
    }

    private void StopStorm()
    {
        if (stormCoroutine != null) StopCoroutine(stormCoroutine);
        stormCoroutine = null;
        SetLightningFlash(0f, 0f);
        if (thunderAudioSource != null) thunderAudioSource.Stop();
    }

    private void SetLightningFlash(float lightMultiplier, float skyMultiplier)
    {
        if (lightningLight != null)
            lightningLight.intensity = lightningIntensity * Mathf.Max(0f, lightMultiplier);

        if (skyController != null)
            skyController.SetLightningFlash(Mathf.Clamp01(skyMultiplier * skyLightningFlashStrength));
    }
    private void RefreshStorm() { if ((rain > stormThreshold || (rain > 0 && SeasonManager.Instance != null && SeasonManager.Instance.currentWeather == WeatherType.Storm)) && stormCoroutine == null) stormCoroutine = StartCoroutine(ThunderstormRoutine()); }
    private IEnumerator ThunderstormRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(Mathf.Max(0.1f, lightningInterval.x), Mathf.Max(lightningInterval.x, lightningInterval.y)));
            SetLightningFlash(1f, 1f);
            yield return new WaitForSeconds(0.1f);
            SetLightningFlash(0f, 0f);
            yield return new WaitForSeconds(0.05f);
            SetLightningFlash(0.5f, 0.55f);
            yield return new WaitForSeconds(0.1f);
            SetLightningFlash(0f, 0f);
            yield return new WaitForSeconds(Random.Range(Mathf.Max(0, thunderDelay.x), Mathf.Max(thunderDelay.x, thunderDelay.y)));
            if (thunderSounds != null && thunderSounds.Length > 0)
            {
                var clip = thunderSounds[Random.Range(0, thunderSounds.Length)];
                if (clip != null) thunderAudioSource.PlayOneShot(clip);
            }
        }
    }
}
