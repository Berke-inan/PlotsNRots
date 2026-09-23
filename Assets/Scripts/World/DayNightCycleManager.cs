using System;
using Newtonsoft.Json.Linq;
using PlotNRots.SaveSystem;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(-300)]
[RequireComponent(typeof(SaveableEntity))]
public sealed class DayNightCycleManager : MonoBehaviour, ISaveable
{
    public static DayNightCycleManager Instance { get; private set; }

    [Header("Time")]
    [Tooltip("Real-world seconds for one full in-game day. Set 0 in Play Mode to freeze time while testing.")]
    [Min(0f)] public float realSecondsPerDay = 1200f;

    [Range(0f, 24f)] public float currentTime = 8f;

    [Header("Gameplay day/night thresholds")]
    [Range(0f, 24f)] public float nightStartTime = 18f;
    [Range(0f, 24f)] public float morningStartTime = 6f;

    public static event Action YeniGunBasladiSinyali;

    [Header("Directional lights")]
    public Light sunLight;
    public Light moonLight;

    [Header("Sun intensity")]
    public AnimationCurve sunIntensity = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(5.5f, 0f),
        new Keyframe(7f, 1.5f),
        new Keyframe(17.5f, 1.5f),
        new Keyframe(19f, 0f),
        new Keyframe(24f, 0f));

    [Header("Ambient intensity")]
    public AnimationCurve ambientIntensityCurve = new AnimationCurve(
        new Keyframe(0f, .55f),
        new Keyframe(6f, .6f),
        new Keyframe(7.5f, 1.2f),
        new Keyframe(17f, 1.2f),
        new Keyframe(18.5f, .6f),
        new Keyframe(24f, .55f));

    [Header("Reflection intensity")]
    public AnimationCurve reflectionIntensityCurve = new AnimationCurve(
        new Keyframe(0f, .3f),
        new Keyframe(6f, .3f),
        new Keyframe(8f, 1.2f),
        new Keyframe(16.5f, 1.2f),
        new Keyframe(18.5f, .3f),
        new Keyframe(24f, .3f));

    [SerializeField] private bool simulateLocally = true;
    [SerializeField, Min(.01f)] private float daylightReferenceIntensity = 1.5f;

    private float weatherLightMultiplier = 1f;
    private float weatherAmbientMultiplier = 1f;

    public Vector3 SunDirection => Quaternion.Euler((currentTime / 24f) * 360f - 90f, 170f, 0f) * Vector3.back;
    public Vector3 MoonDirection => -SunDirection;
    public float NormalizedTime => currentTime / 24f;
    public float Daylight => Mathf.Clamp01(sunIntensity.Evaluate(currentTime) / Mathf.Max(.01f, daylightReferenceIntensity));

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        AdvanceTime();
        UpdateVisuals();
    }

    public void ApplyAtmosphere(
        float directMultiplier,
        float ambientMultiplier,
        Color sunTint,
        Color moonTint,
        Color sky,
        Color equator,
        Color ground)
    {
        weatherLightMultiplier = Mathf.Clamp01(directMultiplier);
        weatherAmbientMultiplier = Mathf.Clamp01(ambientMultiplier);

        if (sunLight != null) sunLight.color = sunTint;
        if (moonLight != null) moonLight.color = moonTint;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = sky;
        RenderSettings.ambientEquatorColor = equator;
        RenderSettings.ambientGroundColor = ground;
        UpdateVisuals();
    }

    public void ClearAtmosphere()
    {
        weatherLightMultiplier = 1f;
        weatherAmbientMultiplier = 1f;
        UpdateVisuals();
    }

    public void SetLocalSimulation(bool value) => simulateLocally = value;

    private void AdvanceTime()
    {
        if (!simulateLocally || realSecondsPerDay <= 0f) return;

        float next = currentTime + Time.deltaTime * 24f / realSecondsPerDay;
        int crossings = Mathf.FloorToInt((next - morningStartTime) / 24f)
                      - Mathf.FloorToInt((currentTime - morningStartTime) / 24f);

        currentTime = Mathf.Repeat(next, 24f);
        for (int i = 0; i < crossings; i++)
            YeniGunBasladiSinyali?.Invoke();
    }

    private void UpdateVisuals()
    {
        float sunAngle = currentTime / 24f * 360f - 90f;

        if (sunLight != null)
        {
            sunLight.transform.rotation = Quaternion.Euler(sunAngle, 170f, 0f);
            sunLight.intensity = sunIntensity.Evaluate(currentTime) * weatherLightMultiplier;
        }

        if (moonLight != null)
        {
            moonLight.transform.rotation = Quaternion.Euler(sunAngle + 180f, 170f, 0f);
            float moonHeight = Mathf.Clamp01(-moonLight.transform.forward.y);
            moonLight.intensity = moonHeight * .8f * weatherLightMultiplier;
        }

        RenderSettings.ambientIntensity = ambientIntensityCurve.Evaluate(currentTime) * weatherAmbientMultiplier;
        RenderSettings.reflectionIntensity = reflectionIntensityCurve.Evaluate(currentTime) * weatherAmbientMultiplier;

        if (sunLight != null && moonLight != null)
            RenderSettings.sun = SunDirection.y >= 0f ? sunLight : moonLight;
    }

    public bool IsNight() => currentTime >= nightStartTime || currentTime <= morningStartTime;

    public void Sleep()
    {
        if (!simulateLocally || !IsNight()) return;
        currentTime = morningStartTime + .5f;
        UpdateVisuals();
        YeniGunBasladiSinyali?.Invoke();
    }

    [Serializable]
    public struct ClockSaveData
    {
        public float time;
    }

    public object SaveState() => new ClockSaveData { time = currentTime };

    public void LoadState(object state)
    {
        ClockSaveData data = state is ClockSaveData typed
            ? typed
            : ((JObject)state).ToObject<ClockSaveData>();

        currentTime = float.IsNaN(data.time) || float.IsInfinity(data.time)
            ? 8f
            : Mathf.Repeat(data.time, 24f);

        UpdateVisuals();
    }
}
