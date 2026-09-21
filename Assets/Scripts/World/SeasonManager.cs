using System;
using UnityEngine;
using UnityEngine.Serialization;
using Newtonsoft.Json.Linq;
using PlotNRots.SaveSystem;

// Keep serialized numeric values and existing event signatures compatible.
public enum Season { Spring, Summer, Autumn, Winter }
public enum WeatherType { Sunny = 0, Cloudy = 1, Rainy = 2, Snowy = 3, PartlyCloudy = 4, Overcast = 5, Storm = 6, SnowStorm = 7, Foggy = 8 }

[Serializable]
public class WeatherProbabilities
{
    [Range(0, 100)] public float sunny;
    [Range(0, 100)] public float cloudy;
    [Range(0, 100)] public float rainy;
    [Range(0, 100)] public float snowy;
}

[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(SaveableEntity), typeof(SnowAccumulationManager))]
public class SeasonManager : MonoBehaviour, ISaveable
{
    public static SeasonManager Instance { get; private set; }
    [SerializeField, Min(1)] private int daysPerSeason = 30;
    [FormerlySerializedAs("currentYear"), SerializeField, Min(1)] private int year = 1;
    [FormerlySerializedAs("currentDay"), SerializeField, Min(1)] private int dayOfSeason = 1;
    [FormerlySerializedAs("currentSeason"), SerializeField] private Season season = Season.Spring;
    [FormerlySerializedAs("currentWeather"), SerializeField] private WeatherType weather = WeatherType.Sunny;
    [SerializeField, Range(0, 1)] private float weatherIntensity = 0.6f;
    [Header("Profiles (Spring, Summer, Autumn, Winter)")]
    [SerializeField] private SeasonWeatherProfile[] seasonProfiles = new SeasonWeatherProfile[4];
    [Header("Legacy scene weights, used if profile is unassigned")]
    [SerializeField] private WeatherProbabilities springWeather;
    [SerializeField] private WeatherProbabilities summerWeather;
    [SerializeField] private WeatherProbabilities autumnWeather;
    [SerializeField] private WeatherProbabilities winterWeather;
    [Header("Authority / reproducible weather")]
    [SerializeField] private bool simulateLocally = true;
    [SerializeField] private int randomState = 17431;
    [SerializeField] private float currentTemperature = 12;
    [SerializeField] private bool temperatureInitialized;
    private SnowAccumulationManager snow;
    private SeasonWeatherProfile ActiveProfile => seasonProfiles != null && (int)season >= 0 && seasonProfiles.Length > (int)season ? seasonProfiles[(int)season] : null;
    public float CurrentTemperature => currentTemperature;
    public float AccumulatedSnowAmount => snow != null ? snow.Amount : 0;
    public static event Action<float> OnTemperatureChanged;

    public int currentYear => year;
    public int currentDay => dayOfSeason;
    public Season currentSeason => season;
    public WeatherType currentWeather => weather;
    public float WeatherIntensity => weatherIntensity;
    public bool SimulateLocally => simulateLocally;
    public int TotalDay => ((year - 1) * 4 + (int)season) * Mathf.Max(1, daysPerSeason) + dayOfSeason;
    public static event Action<Season> OnSeasonChanged;
    // Raised for intensity changes too; subscribers should read WeatherIntensity.
    public static event Action<WeatherType> OnWeatherChanged;
    public static event Action OnStateRestored;
    // Unlike the clock event, this fires AFTER calendar and weather have advanced.
    public static event Action OnDayAdvanced;
    public bool IsRestoringState { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; Destroy(gameObject); return; }
        Instance = this;
        snow = GetComponent<SnowAccumulationManager>();
        if (!temperatureInitialized)
        {
            currentTemperature = DefaultTemperature();
            temperatureInitialized = true;
        }
        // Scene-owned: visuals reference scene particles/lights and must not outlive them.
    }
    private void OnEnable() => DayNightCycleManager.YeniGunBasladiSinyali += AdvanceDay;
    private void OnDisable() => DayNightCycleManager.YeniGunBasladiSinyali -= AdvanceDay;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void AdvanceDay()
    {
        if (!simulateLocally) return;
        if (++dayOfSeason > Mathf.Max(1, daysPerSeason))
        {
            dayOfSeason = 1;
            season = (Season)(((int)season + 1) % 4);
            if (season == Season.Spring) year++;
            OnSeasonChanged?.Invoke(season);
        }
        DetermineWeather();
        OnDayAdvanced?.Invoke();
    }

    private float NextRandom()
    {
        // Independent PRNG; lightning/particles never consume gameplay randomness.
        uint value = randomState == 0 ? 17431u : unchecked((uint)randomState);
        value ^= value << 13; value ^= value >> 17; value ^= value << 5;
        randomState = unchecked((int)value);
        return (value >> 8) * (1f / 16777216f);
    }

    public void DetermineWeather()
    {
        if (!simulateLocally) return;
        float roll = NextRandom();
        float intensityRoll = NextRandom();
        var profile = ActiveProfile;
        float temperatureRoll = NextRandom();
        currentTemperature = profile != null ? profile.SelectTemperature(temperatureRoll, currentTemperature) : DefaultTemperature();
        OnTemperatureChanged?.Invoke(currentTemperature);
        WeatherType selected;
        float intensity;
        if (profile != null) selected = profile.SelectForDay(roll, intensityRoll, weather, season, currentTemperature, out intensity);
        else
        {
            var probabilities = season == Season.Spring ? springWeather : season == Season.Summer ? summerWeather : season == Season.Autumn ? autumnWeather : winterWeather;
            float[] weights = probabilities == null ? new float[4] : new[] { probabilities.sunny, probabilities.cloudy, probabilities.rainy, probabilities.snowy };
            float total = 0;
            foreach (float weight in weights) total += Mathf.Max(0, weight);
            selected = WeatherType.Sunny;
            float remaining = roll * total;
            for (int i = 0; i < weights.Length && total > 0; i++)
            {
                remaining -= Mathf.Max(0, weights[i]);
                if (remaining < 0) { selected = (WeatherType)i; break; }
            }
            intensity = Mathf.Lerp(0.2f, 1, intensityRoll);
        }
        SetWeather(selected, intensity);
    }

    public void SetWeather(WeatherType value, float intensity)
    {
        if (!simulateLocally || !Enum.IsDefined(typeof(WeatherType), value)) return;
        weather = WeatherRules.ResolvePrecipitation(value, season, currentTemperature, ActiveProfile != null ? ActiveProfile.FreezingPoint : 1);
        weatherIntensity = SanitizeIntensity(intensity);
        OnWeatherChanged?.Invoke(weather);
    }

    private float DefaultTemperature() => ActiveProfile != null ? ActiveProfile.TypicalTemperature : season == Season.Winter ? -4 : season == Season.Summer ? 26 : season == Season.Autumn ? 10 : 12;
    public void SetTemperature(float value)
    {
        if (!simulateLocally || float.IsNaN(value) || float.IsInfinity(value)) return;
        currentTemperature = Mathf.Clamp(value, -60, 60);
        temperatureInitialized = true;
        OnTemperatureChanged?.Invoke(currentTemperature);
        SetWeather(weather, weatherIntensity);
    }

    public void SetLocalSimulation(bool enabled) => simulateLocally = enabled;

    [Serializable]
    public class WeatherSaveData
    {
        public int version = 1;
        public int year = 1;
        public Season season;
        public int dayOfSeason = 1;
        public WeatherType currentWeather;
        public float weatherIntensity = 0.6f;
        public float globalSnowAmount;
        public int randomState = 17431;
        public float currentTemperature;
    }

    public object SaveState() => new WeatherSaveData {
        version = 2, currentTemperature = currentTemperature, year = year, season = season, dayOfSeason = dayOfSeason, currentWeather = weather,
        weatherIntensity = weatherIntensity, globalSnowAmount = snow != null ? snow.Amount : 0, randomState = randomState
    };

    public void LoadState(object state)
    {
        var data = state as WeatherSaveData ?? (state as JObject)?.ToObject<WeatherSaveData>();
        if (data != null) ApplySnapshot(data);
    }

    // Future server adapter: disable local simulation AND the clock, then apply server snapshots.
    // No transport/RPC implementation is implied by this API.
    public void ApplySnapshot(WeatherSaveData data)
    {
        if (data == null) return;
        year = Mathf.Max(1, data.year);
        season = Enum.IsDefined(typeof(Season), data.season) ? data.season : Season.Spring;
        dayOfSeason = Mathf.Clamp(data.dayOfSeason, 1, Mathf.Max(1, daysPerSeason));
        weather = Enum.IsDefined(typeof(WeatherType), data.currentWeather) ? data.currentWeather : WeatherType.Sunny;
        weatherIntensity = SanitizeIntensity(data.weatherIntensity);
        randomState = data.randomState;
        currentTemperature = data.version >= 2 && !float.IsNaN(data.currentTemperature) && !float.IsInfinity(data.currentTemperature)
            ? Mathf.Clamp(data.currentTemperature, -60, 60) : DefaultTemperature();
        temperatureInitialized = true;
        // Old saves keep their weather exactly; subsequent simulation follows current climate rules.
        if (snow != null) snow.SetAmount(data.globalSnowAmount);
        IsRestoringState = true;
        try { OnTemperatureChanged?.Invoke(currentTemperature); OnSeasonChanged?.Invoke(season); OnWeatherChanged?.Invoke(weather); OnStateRestored?.Invoke(); }
        finally { IsRestoringState = false; }
    }

    private static float SanitizeIntensity(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Clamp01(value);
}
