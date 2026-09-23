using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SkyWeatherValidation
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
    private static void Call(object target, string method) => target.GetType().GetMethod(method, Fields).Invoke(target, null);
    [MenuItem("Tools/Plots & Rots/Weather/Run sky and climate regression checks")]
    public static void Run()
    {
        WeatherValidation.Run();
        checks = 0;
        var profile = ScriptableObject.CreateInstance<SeasonWeatherProfile>();
        GameObject world = null, clockObject = null, cloudObject = null;
        StylizedSkyController sky = null;
        var oldSky = RenderSettings.skybox;
        var oldFog = RenderSettings.fogColor; float oldDensity = RenderSettings.fogDensity;
        float ambient = RenderSettings.ambientIntensity, reflection = RenderSettings.reflectionIntensity;
        try
        {
            Set(profile, "entries", new[] {
                new SeasonWeatherProfile.Entry { weather = WeatherType.Sunny, weight = 50, maximumIntensity = 1 },
                new SeasonWeatherProfile.Entry { weather = WeatherType.Rainy, weight = 50, maximumIntensity = 1 }
            });
            Check(profile.Select(.6f, .5f, out _) == WeatherType.Rainy, "baseline weighted threshold");
            Check(profile.SelectForDay(.6f, .5f, WeatherType.Sunny, Season.Spring, 12, out _) == WeatherType.Sunny, "previous weather increases persistence weight");
            Check(profile.SelectForDay(.9f, .5f, WeatherType.Sunny, Season.Winter, -4, out _) == WeatherType.Snowy, "cold precipitation resolves to snow");
            Check(WeatherRules.ResolvePrecipitation(WeatherType.SnowStorm, Season.Summer, -20, 1) == WeatherType.Storm, "summer forbids snow even for extreme temperature input");
            Set(profile, "entries", new[] { new SeasonWeatherProfile.Entry { weather = WeatherType.Snowy, weight = 100, maximumIntensity = 1 } });
            Check(profile.SelectForDay(.9f,.5f,WeatherType.Snowy,Season.Summer,20,out _) == WeatherType.Sunny, "summer excludes snow-only profile row");
            Set(profile, "temperatureRange", new Vector2(20,32));
            Check(profile.SelectTemperature(0,26) == 22 && profile.SelectTemperature(1,26) == 30, "daily temperature drift is bounded");
            world = new GameObject("Sky climate fixture") { hideFlags = HideFlags.HideAndDontSave };
            var manager = world.AddComponent<SeasonManager>(); Call(manager,"Awake");
            manager.SetTemperature(23.5f);
            var data = (SeasonManager.WeatherSaveData)manager.SaveState();
            Check(data.version == 2 && data.currentTemperature == 23.5f, "temperature included in v2 save");
            manager.SetTemperature(-4); manager.LoadState(data);
            Check(manager.CurrentTemperature == 23.5f, "v2 load restores temperature");
            manager.ApplySnapshot(new SeasonManager.WeatherSaveData { version = 1, season = Season.Winter, currentWeather = WeatherType.Snowy });
            Check(manager.CurrentTemperature < 0 && manager.currentWeather == WeatherType.Snowy, "legacy winter save gets compatible default temperature without weather reroll");
            manager.SetLocalSimulation(false); manager.SetTemperature(40);
            Check(manager.CurrentTemperature < 0, "client temperature is authoritative");
            clockObject = new GameObject("Sky clock fixture") { hideFlags = HideFlags.HideAndDontSave };
            var clock = clockObject.AddComponent<DayNightCycleManager>();
            clock.currentTime = 12;
            Check(clock.SunDirection.y > .99f && Vector3.Dot(clock.SunDirection,clock.MoonDirection) < -.99f, "opposed solar and lunar orbit");
            clock.currentTime = 23.999f; var before = clock.SunDirection; clock.currentTime = 0;
            Check(Vector3.Distance(before,clock.SunDirection) < .001f, "orbit continuous across midnight");
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Sky/SkyBoxMat.mat");
            Check(RenderSettings.skybox != null, "configured sky material available");
            sky = world.AddComponent<StylizedSkyController>(); Set(sky,"clock",clock);
            var atmosphere = AssetDatabase.LoadAssetAtPath<SkyAtmosphereProfile>("Assets/Settings/Weather/StylizedAtmosphere.asset");
            Check(atmosphere != null && atmosphere.CloudMask != null, "packed cloud mask is assigned through the atmosphere profile");
            Check(atmosphere.CloudMask.wrapMode == TextureWrapMode.Repeat, "cloud mask repeats without a projection seam");
            Set(sky,"profile",atmosphere);
            Call(sky,"OnEnable");
            var state = new WeatherVisuals { cloudColor = Color.white, cloudCoverage = .3f, cirrusAmount = .2f, cloudThickness = .3f,
                fogColor = Color.white, fogDensity = .001f, directLightMultiplier = 1, ambientMultiplier = 1 };
            sky.ApplyWeather(state,.2f);
            Check(sky.RuntimeMaterial.GetFloat("_StarVisibility") > .99f, "night stars visible");
            clock.currentTime = 12; sky.RenderNow();
            Check(sky.RuntimeMaterial.GetFloat("_StarVisibility") == 0 && sky.RuntimeMaterial.GetVector("_SunDir").y > .99f, "noon hides stars and aligns sun disc");
            Check(sky.RuntimeMaterial.GetTexture("_CloudMaskTex") == atmosphere.CloudMask, "runtime material uses profile cloud mask");
            Color clearSky = sky.RuntimeMaterial.GetColor("_SkyTopColor");
            state.darkness = .9f; state.directLightMultiplier = .2f; state.ambientMultiplier = .6f; sky.ApplyWeather(state,1);
            Check(sky.RuntimeMaterial.GetColor("_SkyTopColor").grayscale < clearSky.grayscale, "weather darkness reduces sky brightness");
            Check(RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Trilight, "analytical ambient has no per-frame GI bake");
            Call(sky,"OnDisable"); sky = null;
            Check(RenderSettings.skybox.name == "SkyBoxMat", "runtime clone restored on disable");

            cloudObject = new GameObject("Cloud manager fixture") { hideFlags = HideFlags.HideAndDontSave };
            var cloudManager = cloudObject.AddComponent<StylizedCloudManager>();
            var cloudMeshes = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Models/Clouds/stylize_clouds.glb").OfType<Mesh>().ToArray();
            var cloudMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Sky/Clouds/StylizedCloud.mat");
            Check(cloudMaterial != null && cloudMaterial.enableInstancing, "shared cloud material imports with GPU instancing enabled");
            Check(ShaderUtil.GetShaderMessages(cloudMaterial.shader).All(m => m.severity.ToString() != "Error"), "stylized cloud shader imports without errors");
            Set(cloudManager,"sharedMeshes",cloudMeshes);
            Set(cloudManager,"sharedMaterial",cloudMaterial);
            Set(cloudManager,"anchor",cloudObject.transform);
            Set(cloudManager,"built",false);
            Call(cloudManager,"Awake");
            Check(cloudMeshes.Length >= 3 && cloudManager.PoolCount == 20, "PC cloud pool uses three shared meshes and respects its upper bound");
            var pooledRenderers = cloudObject.GetComponentsInChildren<MeshRenderer>(true);
            Check(pooledRenderers.Length == cloudManager.PoolCount && pooledRenderers.All(r => r.sharedMaterial == cloudMaterial), "all pooled clouds use one shared material");
            Check(cloudObject.GetComponentsInChildren<Collider>(true).Length == 0, "pooled clouds have no colliders");
            Check(cloudObject.GetComponentsInChildren<Rigidbody>(true).Length == 0, "pooled clouds have no rigidbodies");
            Check(cloudObject.GetComponentsInChildren<MonoBehaviour>(true).Length == 1, "pooled clouds have no per-cloud controllers");
            var sunny = new WeatherVisuals { cloudCoverage = .16f, cloudOvercast = 0 };
            cloudManager.ApplyWeather(sunny,.1f);
            Check(cloudManager.TargetVisibleCount >= 4 && cloudManager.TargetVisibleCount <= 8, "Sunny target stays within 4-8 cloud clusters");
            var partly = new WeatherVisuals { cloudCoverage = .38f, cloudOvercast = .05f };
            cloudManager.ApplyWeather(partly,.35f);
            Check(cloudManager.TargetVisibleCount >= 8 && cloudManager.TargetVisibleCount <= 15, "Partly Cloudy target stays within 8-15 cloud clusters");
            var overcast = new WeatherVisuals { cloudCoverage = .92f, cloudOvercast = 1 };
            cloudManager.SetWeather(overcast,.7f);
            cloudManager.Tick(.05f,cloudObject.transform.position);
            Check(cloudManager.IsTransitioning && cloudManager.VisibleCount > 0, "Partly-to-overcast transition fades pooled clouds instead of popping");
            cloudManager.Tick(2,cloudObject.transform.position);
            Check(cloudManager.TargetVisibleCount == 0 && cloudManager.VisibleCount == 0 && cloudManager.CeilingOpacity > .99f, "Overcast activates ceiling and retires fair-weather clusters");
            string sampleScene = File.ReadAllText("Assets/Scenes/SampleScene.unity");
            Check(sampleScene.Split(new[] { "guid: 4a0d8eecb5fd4cb99e88ab87d1a6c901" },StringSplitOptions.None).Length == 2,
                "SampleScene contains exactly one StylizedCloudManager");
            Check(!sampleScene.Contains("guid: e841ee202cc18e848a74467ab2243ca8"), "legacy CloudGenerator is not active in SampleScene");
            Scene loadedSample = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity",OpenSceneMode.Additive);
            var sceneClouds = loadedSample.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<StylizedCloudManager>(true)).ToArray();
            var legacyClouds = loadedSample.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CloudGenerator>(true)).ToArray();
            Check(sceneClouds.Length == 1 && sceneClouds[0].gameObject.name == "SeasonManager", "real SampleScene deserializes one manager on the world owner");
            Check(sceneClouds[0].SharedMaterial == cloudMaterial, "real SampleScene resolves the shared cloud material");
            Call(sceneClouds[0],"Awake");
            Check(sceneClouds[0].PoolCount == 20, "real SampleScene builds the bounded PC pool");
            Check(legacyClouds.Length == 0, "real SampleScene contains no legacy CloudGenerator component");
            EditorSceneManager.CloseScene(loadedSample,true);
            Debug.Log($"Sky/climate regression checks passed: {checks}, plus the weather regression suite.");
        }
        finally
        {
            if (sky != null) Call(sky,"OnDisable");
            if (world != null) UnityEngine.Object.DestroyImmediate(world);
            if (clockObject != null) UnityEngine.Object.DestroyImmediate(clockObject);
            if (cloudObject != null) UnityEngine.Object.DestroyImmediate(cloudObject);
            UnityEngine.Object.DestroyImmediate(profile);
            RenderSettings.skybox = oldSky; RenderSettings.fogColor = oldFog; RenderSettings.fogDensity = oldDensity;
            RenderSettings.ambientIntensity = ambient; RenderSettings.reflectionIntensity = reflection;
        }
    }
}
