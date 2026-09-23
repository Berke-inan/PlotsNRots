using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using PlotNRots.SaveSystem;

// Run outside Play Mode. Throws on failure so -executeMethod can be used in CI.
public static class WeatherValidation
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool success, string message)
    {
        if (!success) throw new Exception("Weather validation failed: " + message);
        checks++;
    }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
    private static void Call(object target, string name) => target.GetType().GetMethod(name, PrivateInstance).Invoke(target, null);

    [MenuItem("Tools/Plots & Rots/Weather/Run regression checks (Edit Mode)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Run these checks outside Play Mode.");
        if (SeasonManager.Instance != null || DayNightCycleManager.Instance != null)
            throw new Exception("Existing runtime singleton detected; leave Play Mode before running checks.");
        float ambient = RenderSettings.ambientIntensity;
        float reflection = RenderSettings.reflectionIntensity;
        float priorSnow = Shader.GetGlobalFloat("_GlobalSnowAmount");
        float priorThreshold = Shader.GetGlobalFloat("_SnowNormalThreshold");
        float priorSoftness = Shader.GetGlobalFloat("_SnowEdgeSoftness");
        Color priorColor = Shader.GetGlobalColor("_GlobalSnowColor");
        checks = 0;
        GameObject world = null, clockObject = null;
        WeatherVisualsManager visuals = null;
        var profile = ScriptableObject.CreateInstance<SeasonWeatherProfile>();
        try
        {
            Set(profile, "entries", new[] {
                new SeasonWeatherProfile.Entry { weather = WeatherType.Sunny, weight = -5 },
                new SeasonWeatherProfile.Entry { weather = WeatherType.Rainy, weight = 3, minimumIntensity = 0.4f, maximumIntensity = 0.8f },
                new SeasonWeatherProfile.Entry { weather = WeatherType.Snowy, weight = 1, minimumIntensity = 0.2f, maximumIntensity = 1 }
            });
            Check(profile.Select(0, 0.5f, out float intensity) == WeatherType.Rainy && Mathf.Approximately(intensity, 0.6f), "negative weight ignored, intensity interpolation");
            Check(profile.Select(0.749f, 0, out _) == WeatherType.Rainy, "weighted lower boundary");
            Check(profile.Select(0.75f, 1, out intensity) == WeatherType.Snowy && intensity == 1, "weighted upper boundary");
            Set(profile, "entries", Array.Empty<SeasonWeatherProfile.Entry>());
            Check(profile.Select(1, 1, out intensity) == WeatherType.Sunny && intensity == 0, "empty profile fallback");

            world = new GameObject("Weather regression fixture") { hideFlags = HideFlags.HideAndDontSave };
            var manager = world.AddComponent<SeasonManager>();
            Call(manager, "Awake");
            var snow = world.GetComponent<SnowAccumulationManager>();
            Call(snow, "Awake");
            manager.ApplySnapshot(new SeasonManager.WeatherSaveData { year = 2, season = Season.Winter, dayOfSeason = 30, currentWeather = WeatherType.Snowy, weatherIntensity = 0.8f, globalSnowAmount = 0.7f, randomState = 999 });
            string json = world.GetComponent<SaveableEntity>().CaptureState();
            manager.SetWeather(WeatherType.Sunny, 0.2f);
            snow.SetAmount(0);
            world.GetComponent<SaveableEntity>().RestoreState(json);
            Check(manager.currentYear == 2 && manager.currentSeason == Season.Winter && manager.currentDay == 30, "calendar JSON roundtrip through SaveableEntity");
            Check(manager.currentWeather == WeatherType.Snowy && Mathf.Approximately(manager.WeatherIntensity, 0.8f), "weather intensity JSON roundtrip");
            Check(Mathf.Approximately(snow.Amount, 0.7f) && Mathf.Approximately(Shader.GetGlobalFloat("_GlobalSnowAmount"), 0.7f), "load immediately publishes snow");
            var saved = (SeasonManager.WeatherSaveData)manager.SaveState();
            manager.DetermineWeather();
            var expectedWeather = manager.currentWeather; var expectedIntensity = manager.WeatherIntensity;
            manager.ApplySnapshot(saved); manager.DetermineWeather();
            Check(manager.currentWeather == expectedWeather && manager.WeatherIntensity == expectedIntensity, "PRNG continuation after restore");
            manager.ApplySnapshot(saved); Call(manager, "AdvanceDay");
            Check(manager.currentYear == 3 && manager.currentSeason == Season.Spring && manager.currentDay == 1, "winter to spring year rollover");
            manager.SetLocalSimulation(false);
            int day = manager.currentDay; var previous = manager.currentWeather;
            Call(manager, "AdvanceDay"); manager.SetWeather(WeatherType.Rainy, 1);
            Check(manager.currentDay == day && manager.currentWeather == previous, "replica does not advance or roll locally");
            manager.ApplySnapshot(new SeasonManager.WeatherSaveData { year = -3, dayOfSeason = -2, season = (Season)99, currentWeather = (WeatherType)99, weatherIntensity = float.NaN });
            Check(manager.currentYear == 1 && manager.currentDay == 1 && manager.currentSeason == Season.Spring && manager.WeatherIntensity == 0, "invalid restored values sanitized");
            snow.SetAmount(-1); Check(snow.Amount == 0, "negative snow clamp");
            snow.SetAmount(20); Check(snow.Amount == 1, "maximum snow clamp");
            manager.SetLocalSimulation(true); manager.SetWeather(WeatherType.Cloudy, 0.5f);
            Check(snow.Amount == 1, "precipitation change preserves accumulated snow");

            snow.SetAmount(0); manager.SetTemperature(-5); manager.SetWeather(WeatherType.Snowy, 0.5f); snow.Simulate(100);
            Check(Mathf.Approximately(snow.Amount, 0.1f), "snow accumulation uses authoritative intensity and elapsed seconds");
            manager.SetWeather(WeatherType.Cloudy, 0.5f); snow.Simulate(100);
            Check(Mathf.Approximately(snow.Amount, 0.1f), "cloudy preserves ground snow");
            manager.SetTemperature(10); manager.SetWeather(WeatherType.Sunny, 0.5f); snow.Simulate(100);
            Check(Mathf.Approximately(snow.Amount, 0.05f), "spring sunshine melts gradually");
            manager.SetLocalSimulation(false); snow.Simulate(100);
            Check(Mathf.Approximately(snow.Amount, 0.05f), "replica snow does not simulate");

            clockObject = new GameObject("Clock regression fixture") { hideFlags = HideFlags.HideAndDontSave };
            var clock = clockObject.AddComponent<DayNightCycleManager>();
            clock.LoadState(new DayNightCycleManager.ClockSaveData { time = 28 });
            Check(clock.currentTime == 4, "clock restore wraps without new day event");
            visuals = world.AddComponent<WeatherVisualsManager>();
            Call(visuals, "Awake");
            var rainObject = new GameObject("Rain fixture"); rainObject.transform.SetParent(world.transform);
            var snowObject = new GameObject("Snow fixture"); snowObject.transform.SetParent(world.transform);
            var rainParticles = rainObject.AddComponent<ParticleSystem>();
            var snowParticles = snowObject.AddComponent<ParticleSystem>();
            Set(visuals, "rainParticles", rainParticles); Set(visuals, "snowParticles", snowParticles);
            Set(visuals, "clock", clock);
            Call(visuals, "OnEnable"); Call(visuals, "Start");
            manager.SetLocalSimulation(true);
            manager.SetWeather(WeatherType.Rainy, 0.4f);
            Check((bool)typeof(WeatherVisualsManager).GetField("transitioning", PrivateInstance).GetValue(visuals), "weather event starts transition");
            manager.ApplySnapshot(new SeasonManager.WeatherSaveData { currentWeather = WeatherType.Snowy, weatherIntensity = 0.7f, globalSnowAmount = 0.65f });
            Check(!(bool)typeof(WeatherVisualsManager).GetField("transitioning", PrivateInstance).GetValue(visuals), "load cancels transition immediately");
            Check(Mathf.Approximately(rainParticles.emission.rateOverTime.constant, 0) && Mathf.Approximately(snowParticles.emission.rateOverTime.constant, 700), "load snaps both precipitation emission rates");
            Check(Mathf.Approximately(Shader.GetGlobalFloat("_GlobalSnowAmount"), 0.65f), "load snaps snow global alongside visuals");
            clock.currentTime = 12; visuals.ApplyWeatherImmediate(); Color dayFog = RenderSettings.fogColor;
            clock.currentTime = 0; Call(visuals, "LateUpdate");
            Check(RenderSettings.fogColor.r < dayFog.r, "settled weather remains day/night modulated");
            Call(visuals, "OnDisable");
            Check(!rainParticles.isPlaying && !snowParticles.isPlaying, "disable stops precipitation");
            visuals = null;
            foreach (string path in new[] {
                "Assets/Art/Shaders/Weather/SnowLit.shader", "Assets/Art/Shaders/Weather/TerrainLit.shader",
                "Assets/Art/Shaders/Weather/TerrainLitAdd.shader", "Assets/Art/Shaders/Weather/TerrainLitBase.shader",
                "Assets/Art/Shaders/Weather/SnowGltf.shadergraph",
                "Assets/FlatShadedShader.shadergraph", "Assets/Art/Sky/LowPolySky.shader" })
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                Check(shader != null, "shader import: " + path);
                var errors = ShaderUtil.GetShaderMessages(shader).Where(m => m.severity.ToString() == "Error").ToArray();
                Check(errors.Length == 0, path + " " + string.Join("; ", errors.Select(e => e.message)));
            }
            Debug.Log($"Weather regression checks passed: {checks}. Shader import checks do not replace rendered Forward/Deferred and Play Mode tests.");
        }
        finally
        {
            if (visuals != null) Call(visuals, "OnDisable");
            if (world != null) UnityEngine.Object.DestroyImmediate(world);
            if (clockObject != null) UnityEngine.Object.DestroyImmediate(clockObject);
            UnityEngine.Object.DestroyImmediate(profile);
            RenderSettings.ambientIntensity = ambient;
            RenderSettings.reflectionIntensity = reflection;
            Shader.SetGlobalFloat("_GlobalSnowAmount", priorSnow);
            Shader.SetGlobalFloat("_SnowNormalThreshold", priorThreshold);
            Shader.SetGlobalFloat("_SnowEdgeSoftness", priorSoftness);
            Shader.SetGlobalColor("_GlobalSnowColor", priorColor);
        }
    }
}
