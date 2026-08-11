using System;
using UnityEngine;

public class DayNightCycleManager : MonoBehaviour
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

    private bool morningTriggered = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Update()
    {
        AdvanceTime();
        UpdateVisuals();
    }

    private void AdvanceTime()
    {
        float timeMultiplier = 24f / realSecondsPerDay;
        currentTime += Time.deltaTime * timeMultiplier;

        if (currentTime >= morningStartTime && currentTime < morningStartTime + 1f && !morningTriggered)
        {
            morningTriggered = true;
            YeniGunBasladiSinyali?.Invoke();
            Debug.Log("Doðal yollarla sabah oldu, yeni gün sinyali gönderildi.");
        }

        if (currentTime >= 24f)
        {
            currentTime = 0f;
            morningTriggered = false;
        }
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
        if (!IsNight()) return;

        currentTime = morningStartTime + 0.5f;
        morningTriggered = true;
        YeniGunBasladiSinyali?.Invoke();
        Debug.Log("Uyudun, yeni gün baþladý.");
    }
}