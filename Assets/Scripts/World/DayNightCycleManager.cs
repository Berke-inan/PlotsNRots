using System;
using UnityEngine;
using PlotNRots.SaveSystem;
using Newtonsoft.Json.Linq;

[DefaultExecutionOrder(-300)]
[RequireComponent(typeof(SaveableEntity))]
public class DayNightCycleManager : MonoBehaviour, ISaveable
{
    public static DayNightCycleManager Instance;

    [Header("Zaman Ayarlarý")]
    [Tooltip("Gerçek hayattaki kaç saniye, oyunda 24 saat sürsün?")]
    public float realSecondsPerDay = 1200f;

    [Range(0f, 24f)]
    public float currentTime = 8f;

    [Header("Gece/Gündüz Geçiþ Saatleri")]
    [Tooltip("Akþam saat kaçta gece sayýlsýn ve farlar yansýn?")]
    public float nightStartTime = 18f;
    [Tooltip("Sabah saat kaçta gündüz sayýlsýn ve farlar sönsün?")]
    public float morningStartTime = 6f;

    public static event Action YeniGunBasladiSinyali;

    [Header("Iþýk Kaynaklarý")]
    public Light sunLight;
    public Light moonLight;

    [Header("Güneþ Þiddeti")]
    public AnimationCurve sunIntensity = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(5.5f, 0f),
        new Keyframe(7f, 1.5f),
        new Keyframe(17.5f, 1.5f),
        new Keyframe(19f, 0f),
        new Keyframe(24f, 0f)
    );

    [Header("Ortam Iþýðý (Yerlerin Kararmasý Ýçin)")]
    public AnimationCurve ambientIntensityCurve = new AnimationCurve(
        new Keyframe(0f, 0.55f),
        new Keyframe(6f, 0.6f),
        new Keyframe(7.5f, 1.2f),
        new Keyframe(17f, 1.2f),
        new Keyframe(18.5f, 0.6f),
        new Keyframe(24f, 0.55f)
    );

    [Header("Yansýma Þiddeti")]
    public AnimationCurve reflectionIntensityCurve = new AnimationCurve(
        new Keyframe(0f, 0.3f),
        new Keyframe(6f, 0.3f),
        new Keyframe(8f, 1.2f),
        new Keyframe(16.5f, 1.2f),
        new Keyframe(18.5f, 0.3f),
        new Keyframe(24f, 0.3f)
    );

    [SerializeField] private bool simulateLocally = true;
    [SerializeField, Min(0.01f)] private float daylightReferenceIntensity = 1.5f;
    public float NormalizedTime => currentTime / 24f;
    public float Daylight => Mathf.Clamp01(sunIntensity.Evaluate(currentTime) / Mathf.Max(0.01f, daylightReferenceIntensity));
    public void SetLocalSimulation(bool value) => simulateLocally = value;
    [Serializable] public struct ClockSaveData { public float time; }
    public object SaveState() => new ClockSaveData { time = currentTime };
    public void LoadState(object state)
    {
        var data = state is ClockSaveData typed ? typed : ((JObject)state).ToObject<ClockSaveData>();
        currentTime = float.IsNaN(data.time) || float.IsInfinity(data.time) ? 8 : Mathf.Repeat(data.time, 24);
        UpdateVisuals();
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; Destroy(this); return; }
        Instance = this;
    }

    private void Update()
    {
        AdvanceTime();
        UpdateVisuals();
    }

    private void AdvanceTime()
    {
        if (!simulateLocally || realSecondsPerDay <= 0) return;
        float next = currentTime + Time.deltaTime * 24f / realSecondsPerDay;
        int crossings = Mathf.FloorToInt((next - morningStartTime) / 24f)
            - Mathf.FloorToInt((currentTime - morningStartTime) / 24f);
        currentTime = Mathf.Repeat(next, 24f);
        for (int i = 0; i < crossings; i++) YeniGunBasladiSinyali?.Invoke();
    }

    private void UpdateVisuals()
    {
        float t = currentTime;
        float sunAngle = (t / 24f) * 360f - 90f;

        if (sunLight != null)
        {
            sunLight.transform.rotation = Quaternion.Euler(sunAngle, 170f, 0f);
            sunLight.intensity = sunIntensity.Evaluate(t);
        }

        if (moonLight != null)
        {
            // ÝÞTE DÜZELTÝLEN SATIR: Yanlýþlýkla sunLight yazýlan yer moonLight olarak deðiþtirildi.
            moonLight.transform.rotation = Quaternion.Euler(sunAngle + 180f, 170f, 0f);
            float moonHeight = Mathf.Clamp01(-moonLight.transform.forward.y);
            moonLight.intensity = moonHeight * 0.8f;
        }

        RenderSettings.ambientIntensity = ambientIntensityCurve.Evaluate(t);
        RenderSettings.reflectionIntensity = reflectionIntensityCurve.Evaluate(t);

        if (RenderSettings.skybox != null)
        {
            if (sunLight != null && RenderSettings.skybox.HasProperty("_SunDir"))
                RenderSettings.skybox.SetVector("_SunDir", -sunLight.transform.forward);

            if (moonLight != null && RenderSettings.skybox.HasProperty("_MoonDir"))
                RenderSettings.skybox.SetVector("_MoonDir", -moonLight.transform.forward);
        }
    }

    public bool IsNight() => currentTime >= nightStartTime || currentTime <= morningStartTime;

    public void Sleep()
    {
        if (!simulateLocally || !IsNight()) return;

        currentTime = morningStartTime + 0.5f;
        UpdateVisuals();
        YeniGunBasladiSinyali?.Invoke();
        Debug.Log("Uyudun, yeni gün baþladý.");
    }
}