using System;
using UnityEngine;

// One world-level simulation, no renderer searches or per-object components.
[DisallowMultipleComponent]
public class SnowAccumulationManager : MonoBehaviour
{
    [SerializeField, Min(0)] private float accumulationPerSecond = 0.002f;
    [SerializeField, Min(0)] private float meltPerSecond = 0.0005f;
    [SerializeField, Range(0, 1)] private float maximumAmount = 1;
    [SerializeField, Range(0, 1)] private float normalThreshold = 0.45f;
    [SerializeField, Range(0, 0.5f)] private float edgeSoftness = 0.05f;
    [SerializeField] private Color snowColor = new Color(0.88f, 0.94f, 1);
    [SerializeField, Range(0, 1)] private float amount;
    [SerializeField] private float meltTemperature = 0;
    [SerializeField, Min(0.1f)] private float fullMeltTemperature = 10;
    private SeasonManager season;
    public float Amount => amount;
    public event Action<float> OnSnowAmountChanged;

    private void Awake() => season = GetComponent<SeasonManager>();
    private void OnEnable() => Publish();
    private void OnDisable() => Shader.SetGlobalFloat("_GlobalSnowAmount", 0);

    private void Update() => Simulate(Time.deltaTime);

    public void Simulate(float seconds)
    {
        if (seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
        if (season == null || !season.SimulateLocally) return;
        float warmth = Mathf.InverseLerp(meltTemperature, Mathf.Max(meltTemperature + 0.1f, fullMeltTemperature), season.CurrentTemperature);
        float rate = WeatherRules.IsSnow(season.currentWeather)
            ? accumulationPerSecond * season.WeatherIntensity * (1 - warmth)
            : -meltPerSecond * warmth;
        SetAmount(amount + rate * seconds);
    }

    public void SetAmount(float value)
    {
        float next = Mathf.Clamp(float.IsNaN(value) || float.IsInfinity(value) ? 0 : value, 0, maximumAmount);
        bool changed = !Mathf.Approximately(amount, next);
        amount = next;
        Publish(); // Also snaps globals when loading the same value.
        if (changed) OnSnowAmountChanged?.Invoke(amount);
    }

    private void Publish()
    {
        Shader.SetGlobalFloat("_GlobalSnowAmount", amount);
        Shader.SetGlobalFloat("_SnowNormalThreshold", normalThreshold);
        Shader.SetGlobalFloat("_SnowEdgeSoftness", edgeSoftness);
        Shader.SetGlobalColor("_GlobalSnowColor", snowColor);
    }
}
