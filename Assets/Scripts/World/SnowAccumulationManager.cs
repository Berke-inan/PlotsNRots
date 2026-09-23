using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SnowAccumulationManager : MonoBehaviour
{
    [Header("Accumulation")]

    [SerializeField, Min(0f)]
    private float accumulationPerSecond = .002f;

    [SerializeField, Min(0f)]
    private float meltPerSecond = .0005f;

    [SerializeField, Range(0f, 1f)]
    private float maximumAmount = 1f;

    [SerializeField, Range(0f, 1f)]
    private float amount;


    [Header("Surface Coverage")]

    [SerializeField, Range(0f, 1f)]
    private float normalThreshold = .40f;

    [SerializeField, Range(0f, .5f)]
    private float edgeSoftness = .18f;

    [SerializeField]
    private Color snowColor =
        new Color(
            .92f,
            .955f,
            1f,
            1f);


    [Header("Snow Surface")]

    [Tooltip(
        "Tam birikimde terrain üzerindeki görsel kar kalýnlýðý. " +
        "0.055 yaklaþýk 5.5 cm.")]
    [SerializeField, Range(0f, .15f)]
    private float maximumVisualThickness = .055f;

    [SerializeField, Range(.1f, 8f)]
    private float detailScale = 1.45f;

    [SerializeField, Range(0f, 1f)]
    private float detailStrength = .36f;

    [SerializeField, Range(0f, 1f)]
    private float normalDetailStrength = .42f;

    [SerializeField, Range(0f, 1f)]
    private float snowSmoothness = .16f;


    [Header("Melting")]

    [SerializeField]
    private float meltTemperature = 0f;

    [SerializeField, Min(.1f)]
    private float fullMeltTemperature = 10f;


    private SeasonManager season;


    public float Amount =>
        amount;


    public event Action<float>
        OnSnowAmountChanged;


    private void Awake()
    {
        season =
            GetComponent<
                SeasonManager>();
    }


    private void OnEnable()
    {
        Publish();
    }


    private void OnDisable()
    {
        Shader.SetGlobalFloat(
            "_GlobalSnowAmount",
            0f);

        Shader.SetGlobalFloat(
            "_GlobalSnowThickness",
            0f);
    }


    private void Update()
    {
        Simulate(
            Time.deltaTime);
    }


    public void Simulate(
        float seconds)
    {
        if (seconds <= 0f
            ||
            float.IsNaN(seconds)
            ||
            float.IsInfinity(seconds))
        {
            return;
        }


        if (season == null
            ||
            !season.SimulateLocally)
        {
            return;
        }


        float warmth =
            Mathf.InverseLerp(
                meltTemperature,

                Mathf.Max(
                    meltTemperature + .1f,
                    fullMeltTemperature),

                season.CurrentTemperature);


        float rate =
            WeatherRules.IsSnow(
                season.currentWeather)

                ?
                accumulationPerSecond
                *
                season.WeatherIntensity
                *
                (1f - warmth)

                :
                -meltPerSecond
                *
                warmth;


        SetAmount(
            amount
            +
            rate
            *
            seconds);
    }


    public void SetAmount(
        float value)
    {
        float safe =
            float.IsNaN(value)
            ||
            float.IsInfinity(value)

                ?
                0f

                :
                value;


        float next =
            Mathf.Clamp(
                safe,
                0f,
                maximumAmount);


        bool changed =
            !Mathf.Approximately(
                amount,
                next);


        amount =
            next;


        Publish();


        if (changed)
        {
            OnSnowAmountChanged?.Invoke(
                amount);
        }
    }


    private void Publish()
    {
        Shader.SetGlobalFloat(
            "_GlobalSnowAmount",
            amount);


        Shader.SetGlobalFloat(
            "_SnowNormalThreshold",
            normalThreshold);


        Shader.SetGlobalFloat(
            "_SnowEdgeSoftness",
            edgeSoftness);


        Shader.SetGlobalColor(
            "_GlobalSnowColor",
            snowColor);


        Shader.SetGlobalFloat(
            "_GlobalSnowThickness",
            maximumVisualThickness);


        Shader.SetGlobalFloat(
            "_GlobalSnowDetailScale",
            detailScale);


        Shader.SetGlobalFloat(
            "_GlobalSnowDetailStrength",
            detailStrength);


        Shader.SetGlobalFloat(
            "_GlobalSnowNormalStrength",
            normalDetailStrength);


        Shader.SetGlobalFloat(
            "_GlobalSnowSmoothness",
            snowSmoothness);
    }


#if UNITY_EDITOR

    private void OnValidate()
    {
        maximumAmount =
            Mathf.Clamp01(
                maximumAmount);


        amount =
            Mathf.Clamp(
                amount,
                0f,
                maximumAmount);


        if (isActiveAndEnabled)
        {
            Publish();
        }
    }

#endif
}