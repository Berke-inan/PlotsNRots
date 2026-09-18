using System.Collections;
using UnityEngine;
using PlotNRots.SaveSystem;

[System.Serializable]
public struct WeatherVisuals
{
    [Header("Gökyüzü Ayarlarý")]
    public Color skyTopColor;
    public Color skyBottomColor;
    public Color cloudColor;
    [Range(0f, 1f)] public float cloudCoverage;

    [Header("Atmosfer Ayarlarý")]
    public float fogDensity;
    public Color fogColor;
}

public class WeatherVisualsManager : MonoBehaviour
{
    [Header("Dinamik Yaðýþ Efektleri")]
    [SerializeField] private ParticleSystem rainParticles;
    [SerializeField] private ParticleSystem snowParticles;

    [SerializeField] private float maxRainEmission = 1500f;
    [SerializeField] private float maxSnowEmission = 1000f;

    [Header("Yaðmur Sesi Ayarlarý")]
    [Tooltip("Döngüye girecek (Loop) þiddetli yaðmur sesi")]
    [SerializeField] private AudioClip rainSoundClip;
    [Tooltip("En þiddetli fýrtýnada yaðmurun çýkacaðý maksimum ses seviyesi")]
    [SerializeField, Range(0f, 1f)] private float maxRainVolume = 0.8f;

    private AudioSource rainAudioSource;


    [Header("Gök Gürültüsü ve Þimþek")]
    [SerializeField] private Light lightningLight;
    [SerializeField] private AudioClip[] thunderSounds;
    private AudioSource thunderAudioSource;

    [Header("Geçiþ Ayarlarý")]


    // --- ÖN TANIMLI RENK VE GÖRSELLÝK AYARLARI ---
    [SerializeField] private WeatherVisuals sunnyVisuals = new WeatherVisuals
    {
        skyTopColor = new Color(0.32f, 0.65f, 0.87f),
        skyBottomColor = new Color(0.64f, 0.84f, 0.96f),
        cloudColor = new Color(1f, 1f, 1f),
        cloudCoverage = 0.25f,
        fogDensity = 0.0005f,
        fogColor = new Color(0.7f, 0.85f, 0.95f)
    };

    [SerializeField] private WeatherVisuals cloudyVisuals = new WeatherVisuals
    {
        skyTopColor = new Color(0.46f, 0.54f, 0.6f),
        skyBottomColor = new Color(0.65f, 0.71f, 0.75f),
        cloudColor = new Color(0.85f, 0.85f, 0.85f),
        cloudCoverage = 0.55f,
        fogDensity = 0.005f,
        fogColor = new Color(0.65f, 0.71f, 0.75f)
    };

    [SerializeField] private WeatherVisuals rainyVisuals = new WeatherVisuals
    {
        skyTopColor = new Color(0.23f, 0.26f, 0.3f),
        skyBottomColor = new Color(0.3f, 0.34f, 0.38f),
        cloudColor = new Color(0.25f, 0.28f, 0.32f),
        cloudCoverage = 0.85f,
        fogDensity = 0.008f,
        fogColor = new Color(0.3f, 0.34f, 0.38f)
    };

    [SerializeField] private WeatherVisuals snowyVisuals = new WeatherVisuals
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
    [Header("Storm")]
    [SerializeField, Range(0, 1)] private float stormThreshold = 0.6f;
    [SerializeField] private Vector2 lightningInterval = new Vector2(10, 30);
    [SerializeField] private Vector2 thunderDelay = new Vector2(1, 3);
    [SerializeField, Min(0)] private float lightningIntensity = 3;

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
        if (originalSky != null)
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
        if (runtimeSky != null) Destroy(runtimeSky);
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
        target = weather == WeatherType.Rainy ? rainyVisuals : weather == WeatherType.Snowy ? snowyVisuals : weather == WeatherType.Cloudy ? cloudyVisuals : sunnyVisuals;
        targetRain = weather == WeatherType.Rainy ? intensity : 0;
        targetSnow = weather == WeatherType.Snowy ? intensity : 0;
        targetWind = weather == WeatherType.Sunny ? 0.1f : Mathf.Lerp(0.2f, 1, intensity);
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
        ApplyVisuals();
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
    private static WeatherVisuals Blend(WeatherVisuals a, WeatherVisuals b, float t) => new WeatherVisuals {
        skyTopColor = Color.Lerp(a.skyTopColor, b.skyTopColor, t), skyBottomColor = Color.Lerp(a.skyBottomColor, b.skyBottomColor, t),
        cloudColor = Color.Lerp(a.cloudColor, b.cloudColor, t), cloudCoverage = Mathf.Lerp(a.cloudCoverage, b.cloudCoverage, t),
        fogColor = Color.Lerp(a.fogColor, b.fogColor, t), fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, t)
    };
    private void ApplyVisuals()
    {
        float brightness = Mathf.Lerp(nightBrightness, 1, clock != null ? clock.Daylight : 1);
        RenderSettings.fogColor = Dim(current.fogColor, brightness);
        RenderSettings.fogDensity = current.fogDensity;
        SetSkyColor("_SkyTopColor", Dim(current.skyTopColor, brightness));
        SetSkyColor("_SkyBottomColor", Dim(current.skyBottomColor, brightness));
        SetSkyColor("_CloudColor", Dim(current.cloudColor, brightness));
        if (runtimeSky != null && runtimeSky.HasProperty("_CloudCoverage")) runtimeSky.SetFloat("_CloudCoverage", current.cloudCoverage);
        if (runtimeSky != null && runtimeSky.HasProperty("_CloudOffset"))
        {
            runtimeSky.SetFloat("_CloudSpeed", 0);
            runtimeSky.SetFloat("_CloudOffset", cloudOffset);
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
        var emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = maximum * intensity;
        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = wind * maximumParticleDrift;
        if (intensity > 0.001f && !particles.isPlaying) particles.Play();
        else if (intensity <= 0.001f && particles.isPlaying) particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
    private void StopStorm()
    {
        if (stormCoroutine != null) StopCoroutine(stormCoroutine);
        stormCoroutine = null;
        if (lightningLight != null) lightningLight.intensity = 0;
        if (thunderAudioSource != null) thunderAudioSource.Stop();
    }
    private void RefreshStorm() { if (rain > stormThreshold && stormCoroutine == null) stormCoroutine = StartCoroutine(ThunderstormRoutine()); }
    private IEnumerator ThunderstormRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(Mathf.Max(0.1f, lightningInterval.x), Mathf.Max(lightningInterval.x, lightningInterval.y)));
            if (lightningLight != null)
            {
                lightningLight.intensity = lightningIntensity;
                yield return new WaitForSeconds(0.1f);
                lightningLight.intensity = 0;
                yield return new WaitForSeconds(0.05f);
                lightningLight.intensity = lightningIntensity * 0.5f;
                yield return new WaitForSeconds(0.1f);
                lightningLight.intensity = 0;
            }
            yield return new WaitForSeconds(Random.Range(Mathf.Max(0, thunderDelay.x), Mathf.Max(thunderDelay.x, thunderDelay.y)));
            if (thunderSounds != null && thunderSounds.Length > 0)
            {
                var clip = thunderSounds[Random.Range(0, thunderSounds.Length)];
                if (clip != null) thunderAudioSource.PlayOneShot(clip);
            }
        }
    }
}
