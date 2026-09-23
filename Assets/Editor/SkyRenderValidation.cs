using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Render an isolated, deliberately simple test stage through the project's PC URP renderer.
// Does not open, save, or modify the user's gameplay scene.
public static class SkyRenderValidation
{
    private static int frames;
    private static Scene stage, previousScene;
    private static GameObject root;
    private static Camera camera;
    private static DayNightCycleManager clock;
    private static StylizedSkyController sky;
    private static StylizedCloudManager clouds;
    private static WeatherAppearanceProfile appearance;
    private static RenderPipelineAsset previousPipeline, previousQuality;
    private static Material oldSky;
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object target, string field, object value) => target.GetType().GetField(field,Fields).SetValue(target,value);
    private static void Call(object target, string method) => target.GetType().GetMethod(method,Fields).Invoke(target,null);

    public static void CaptureBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run this renderer only in an isolated batch validation project.");
        try
        {
            SkyWeatherValidation.Run();
            previousScene = SceneManager.GetActiveScene();
            oldSky = RenderSettings.skybox;
            previousPipeline = GraphicsSettings.defaultRenderPipeline; previousQuality = QualitySettings.renderPipeline;
            var mode = string.IsNullOrEmpty(previousScene.path) ? NewSceneMode.Single : NewSceneMode.Additive;
            stage = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            SceneManager.SetActiveScene(stage);
            GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            QualitySettings.renderPipeline = null;
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Sky/SkyBoxMat.mat");
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            root = new GameObject("Sky render validation stage");
            clock = root.AddComponent<DayNightCycleManager>();
            clock.SetLocalSimulation(false);
            var sun = new GameObject("Test Sun"); sun.transform.SetParent(root.transform); clock.sunLight = sun.AddComponent<Light>(); clock.sunLight.type = LightType.Directional;
            var moon = new GameObject("Test Moon"); moon.transform.SetParent(root.transform); clock.moonLight = moon.AddComponent<Light>(); clock.moonLight.type = LightType.Directional;
            sky = root.AddComponent<StylizedSkyController>(); Set(sky,"clock",clock);
            Set(sky,"profile",AssetDatabase.LoadAssetAtPath<SkyAtmosphereProfile>("Assets/Settings/Weather/StylizedAtmosphere.asset"));
            typeof(StylizedSkyController).GetMethod("OnEnable",Fields).Invoke(sky,null);
            appearance = AssetDatabase.LoadAssetAtPath<WeatherAppearanceProfile>("Assets/Settings/Weather/WeatherAppearance.asset");
            var cameraObject = new GameObject("Capture Camera"); cameraObject.transform.SetParent(root.transform);
            camera = cameraObject.AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.Skybox; camera.fieldOfView = 85; camera.farClipPlane = 3000;
            camera.transform.position = new Vector3(0,7,0);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            clouds = root.AddComponent<StylizedCloudManager>();
            Set(clouds,"sharedMeshes",AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Models/Clouds/stylize_clouds.glb").OfType<Mesh>().ToArray());
            Set(clouds,"sharedMaterial",AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Sky/Clouds/StylizedCloud.mat"));
            Set(clouds,"anchor",camera.transform);
            Set(clouds,"built",false);
            Call(clouds,"Awake");
            // Low-poly silhouettes keep the horizon readable while exposing most of the sky.
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color = new Color(.16f,.24f,.2f); material.SetFloat("_Smoothness",0);
            for (int i = 0; i < 18; i++)
            {
                var hill = GameObject.CreatePrimitive(PrimitiveType.Cube); hill.name = "Test hill"; hill.transform.SetParent(root.transform);
                float angle = i * 20 * Mathf.Deg2Rad;
                hill.transform.position = new Vector3(Mathf.Sin(angle)*170,-40,Mathf.Cos(angle)*170);
                hill.transform.localScale = new Vector3(100,35 + (i%4)*5,75); hill.transform.rotation = Quaternion.Euler(0,i*20,15);
                hill.GetComponent<Renderer>().sharedMaterial = material;
            }
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.transform.SetParent(root.transform); ground.transform.localScale = Vector3.one*100;
            ground.GetComponent<Renderer>().sharedMaterial = material;
            frames = 0;
            EditorApplication.update += Tick;
        }
        catch (Exception ex) { Debug.LogException(ex); Finish(1); }
    }
    private static void Tick()
    {
        if (++frames < 8) return;
        EditorApplication.update -= Tick;
        try
        {
            string output = Path.GetFullPath("SkyCaptures"); Directory.CreateDirectory(output);
            foreach (string stale in Directory.GetFiles(output,"*.png")) File.Delete(stale);
            Capture(output,"01-sunny-noon",12,WeatherType.Sunny,0,false);
            Capture(output,"02-partly-cloudy-noon",12,WeatherType.PartlyCloudy,45,false);
            Capture(output,"03-sunrise",6.15f,WeatherType.PartlyCloudy,0,true);
            Capture(output,"04-sunset",17.85f,WeatherType.PartlyCloudy,0,true);
            Capture(output,"05-clear-night",22,WeatherType.Sunny,180,true);
            Capture(output,"06-partly-cloudy-night",22,WeatherType.PartlyCloudy,225,false);
            Capture(output,"07-overcast-day",13,WeatherType.Overcast,270,false);
            Capture(output,"08-rain-day",14,WeatherType.Rainy,315,false);
            Capture(output,"09-storm-day",15,WeatherType.Storm,90,false);
            Capture(output,"10-snow-morning",8,WeatherType.Snowy,135,false);
            Debug.Log("Sky render captures written to " + output);
            Finish(0);
        }
        catch(Exception ex) { Debug.LogException(ex); Finish(1); }
    }
    private static void Capture(string output, string name, float time, WeatherType weather, float yaw, bool faceCelestial)
    {
        clock.currentTime = time;
        appearance.TryGet(weather,out var state); sky.ApplyWeather(state,state.windStrength);
        clouds.ApplyWeather(state,state.windStrength);
        clouds.Tick(0,camera.transform.position);
        if (faceCelestial)
        {
            Vector3 celestial = time > 19 || time < 5 ? clock.MoonDirection : clock.SunDirection;
            Vector3 horizontal = new Vector3(celestial.x,0,celestial.z);
            if (horizontal.sqrMagnitude < .0001f) horizontal = Vector3.forward;
            camera.transform.rotation = Quaternion.LookRotation(horizontal.normalized) * Quaternion.Euler(-20,0,0);
        }
        else camera.transform.rotation = Quaternion.Euler(-22,yaw,0);
        var rt = new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);
        rt.Create();
        camera.targetTexture = rt;
        // Explicit URP single-camera render request; no screenshot or external image fabrication.
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
        RenderPipeline.SubmitRenderRequest(camera,request);
        var old = RenderTexture.active; RenderTexture.active = rt;
        var texture = new Texture2D(1280,720,TextureFormat.RGB24,false);
        texture.ReadPixels(new Rect(0,0,1280,720),0,0); texture.Apply();
        var pixels = texture.GetPixels32();
        int minimum = 255, maximum = 0;
        foreach (var pixel in pixels) { minimum = Math.Min(minimum, pixel.r); maximum = Math.Max(maximum, pixel.r); }
        if (maximum - minimum < 12) throw new Exception("Empty/uniform render: " + name);
        File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());
        RenderTexture.active = old; camera.targetTexture = null; rt.Release();
        UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(rt);
    }
    private static void Finish(int code)
    {
        EditorApplication.update -= Tick;
        if (sky != null) typeof(StylizedSkyController).GetMethod("OnDisable",Fields).Invoke(sky,null);
        if (root != null) UnityEngine.Object.DestroyImmediate(root);
        if (stage.IsValid()) EditorSceneManager.CloseScene(stage,true);
        if (previousScene.IsValid()) SceneManager.SetActiveScene(previousScene);
        RenderSettings.skybox = oldSky;
        GraphicsSettings.defaultRenderPipeline = previousPipeline; QualitySettings.renderPipeline = previousQuality;
        if (Application.isBatchMode) EditorApplication.Exit(code);
    }
}
